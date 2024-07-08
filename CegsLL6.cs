using System.ComponentModel;
using System.Linq;
using static AeonHacs.Components.CegsPreferences;

namespace AeonHacs.Components
{
    public partial class CegsLL6 : Cegs
    {
        #region HacsComponent

        [HacsPreConnect]
        protected virtual void PreConnect()
        {
            #region Logs

            SampleLog = Find<HacsLog>("SampleLog");
            TestLog = Find<HacsLog>("TestLog");

            VM1PressureLog = Find<DataLog>("VM1PressureLog");
            VM1PressureLog.Changed = (col) => col.Resolution > 0 && col.Source is Manometer m ?
                (col.PriorValue is double p ?
                    Manometer.SignificantChange(p, m.Pressure) :
                    true) :
                false;

            AmbientLog = Find<DataLog>("AmbientLog");
            // These components are needed to allow the inclusion of
            // non-INamedValue properties of theirs in logged data.
            HeaterController1 = Find<HC6Controller>("HeaterController1");
            HeaterController2 = Find<HC6Controller>("HeaterController2");
            HeaterController3 = Find<HC6Controller>("HeaterController3");
            HeaterController4 = Find<HC6Controller>("HeaterController4");
            AmbientLog.AddNewValue("HC1.CJ", -1, "0.0",
                () => HeaterController1.ColdJunctionTemperature);
            AmbientLog.AddNewValue("HC2.CJ", -1, "0.0",
                () => HeaterController2.ColdJunctionTemperature);
            AmbientLog.AddNewValue("HC3.CJ", -1, "0.0",
                () => HeaterController3.ColdJunctionTemperature);
            AmbientLog.AddNewValue("HC4.CJ", -1, "0.0",
                () => HeaterController4.ColdJunctionTemperature);


            GRSTLog = Find<DataLog>("GRSampleTemperatureLog");
            // These components are needed to allow the inclusion of
            // non-INamedValue properties of theirs in logged data.
            GR1 = Find<GraphiteReactor>("GR1");
            GR2 = Find<GraphiteReactor>("GR2");
            GR3 = Find<GraphiteReactor>("GR3");
            GR4 = Find<GraphiteReactor>("GR4");
            GR5 = Find<GraphiteReactor>("GR5");
            GR6 = Find<GraphiteReactor>("GR6");
            GRSTLog.AddNewValue("GR1.SampleTemperature", 1, "0.0",
                () => GR1.SampleTemperature);
            GRSTLog.AddNewValue("GR2.SampleTemperature", 1, "0.0",
                () => GR2.SampleTemperature);
            GRSTLog.AddNewValue("GR3.SampleTemperature", 1, "0.0",
                () => GR3.SampleTemperature);
            GRSTLog.AddNewValue("GR4.SampleTemperature", 1, "0.0",
                () => GR4.SampleTemperature);
            GRSTLog.AddNewValue("GR5.SampleTemperature", 1, "0.0",
                () => GR5.SampleTemperature);
            GRSTLog.AddNewValue("GR6.SampleTemperature", 1, "0.0",
                () => GR6.SampleTemperature);

            #endregion Logs
        }

        [HacsConnect]
        protected override void Connect()
        {
            base.Connect();

            #region a Cegs needs these
            // The base Cegs really can't do "carbon extraction and graphitization"
            // unless these objects are defined.

            Power = Find<Power>("Power");
            Ambient = Find<Chamber>("Ambient");
            VacuumSystem1 = Find<VacuumSystem>("VacuumSystem1");

            IM = Find<Section>("IM");
            VTT = Find<Section>("VTT");
            MC = Find<Section>("MC");
            Split = Find<Section>("Split");
            GM = Find<Section>("GM");

            VTT_MC = Find<Section>("VTT_MC");
            MC_Split = Find<Section>("MC_Split");

            ugCinMC = Find<Meter>("ugCinMC");

            InletPorts = CachedList<IInletPort>();
            GraphiteReactors = CachedList<IGraphiteReactor>();
            d13CPorts = CachedList<Id13CPort>();

            #endregion a Cegs needs these

            CT = Find<Section>("CT");
            IM_CT = Find<Section>("IM_CT");
            CT_VTT = Find<Section>("CT_VTT");
            MC_GM = Find<Section>("MC_GM");

        }
        #endregion HacsComponent

        #region System configuration
        #region HacsComponents
        public DataLog GRSTLog { get; set; }
        public GraphiteReactor GR1 { get; set; }
        public GraphiteReactor GR2 { get; set; }
        public GraphiteReactor GR3 { get; set; }
        public GraphiteReactor GR4 { get; set; }
        public GraphiteReactor GR5 { get; set; }
        public GraphiteReactor GR6 { get; set; }

        public HC6Controller HeaterController1 { get; set; }
        public HC6Controller HeaterController2 { get; set; }
        public HC6Controller HeaterController3 { get; set; }
        public HC6Controller HeaterController4 { get; set; }
        public virtual double umolCinMC => ugCinMC.Value / gramsCarbonPerMole;
        public virtual ISection IM_CT { get; set; }
        public virtual ISection CT_VTT { get; set; }
        public virtual ISection MC_GM { get; set; }

        #endregion HacsComponents
        #endregion System configuration

        #region Periodic system activities & maintenance

        protected override void ZeroPressureGauges()
        {
            base.ZeroPressureGauges();

            // do not auto-zero pressure gauges while a process is running
            if (Busy) return;

            // ensure baseline VM pressure & steady state
            if (VacuumSystem1.TimeAtBaseline.TotalSeconds < 20)
                return;

            if (MC?.PathToVacuum?.IsOpened() ?? false)
                ZeroIfNeeded(MC?.Manometer, 15);

            if (CT?.PathToVacuum?.IsOpened() ?? false)
                ZeroIfNeeded(CT?.Manometer, 5);

            if (IM?.PathToVacuum?.IsOpened() ?? false)
                ZeroIfNeeded(IM?.Manometer, 10);

            if (GM?.PathToVacuum?.IsOpened() ?? false)
            {
                ZeroIfNeeded(GM?.Manometer, 10);
                foreach (var gr in GraphiteReactors)
                    if (Manifold(gr).PathToVacuum.IsOpened() && gr.IsOpened)
                        ZeroIfNeeded(gr.Manometer, 5);
            }
        }

        #endregion Periodic system activities & maintenance

        #region Process Management

        protected override void BuildProcessDictionary()
        {
            Separators.Clear();

            // Running samples
            ProcessDictionary["Run samples"] = RunSamples;
            Separators.Add(ProcessDictionary.Count);

            // Preparation for running samples
            ProcessDictionary["Prepare GRs for new iron and desiccant"] = PrepareGRsForService;
            ProcessDictionary["Precondition GR iron"] = PreconditionGRs;
            ProcessDictionary["Replace iron in sulfur traps"] = ChangeSulfurFe;
            //ProcessDictionary["Service d13C ports"] = Service_d13CPorts;
            //ProcessDictionary["Load empty d13C ports"] = LoadEmpty_d13CPorts;
            //ProcessDictionary["Prepare loaded d13C ports"] = PrepareLoaded_d13CPorts;
            ProcessDictionary["Prepare loaded inlet ports for collection"] = PrepareIPsForCollection;
            Separators.Add(ProcessDictionary.Count);

            ProcessDictionary["Prepare carbonate sample for acid"] = PrepareCarbonateSample;
            ProcessDictionary["Load acidified carbonate sample"] = LoadCarbonateSample;
            Separators.Add(ProcessDictionary.Count);

            // Open line
            ProcessDictionary["Open and evacuate line"] = OpenLine;
            Separators.Add(ProcessDictionary.Count);

            // Main process continuations
            ProcessDictionary["Collect, etc."] = CollectEtc;
            ProcessDictionary["Extract, etc."] = ExtractEtc;
            ProcessDictionary["Measure, etc."] = MeasureEtc;
            ProcessDictionary["Graphitize, etc."] = GraphitizeEtc;
            Separators.Add(ProcessDictionary.Count);

            // Top-level steps for main process sequence
            ProcessDictionary["Admit sealed CO2 to InletPort"] = AdmitSealedCO2IP;
            ProcessDictionary["Collect CO2 from InletPort"] = Collect;
            ProcessDictionary["Extract"] = Extract;
            ProcessDictionary["Measure"] = Measure;
            ProcessDictionary["Discard excess CO2 by splits"] = DiscardSplit;
            ProcessDictionary["Remove sulfur"] = RemoveSulfur;
            ProcessDictionary["Dilute small sample"] = Dilute;
            ProcessDictionary["Graphitize aliquots"] = GraphitizeAliquots;
            Separators.Add(ProcessDictionary.Count);

            // Secondary-level process sub-steps
            ProcessDictionary["Evacuate Inlet Port"] = EvacuateIP;
            ProcessDictionary["Flush Inlet Port"] = FlushIP;
            ProcessDictionary["Admit O2 into Inlet Port"] = AdmitIPO2;
            ProcessDictionary["Heat Quartz and Open Line"] = HeatQuartzOpenLine;
            ProcessDictionary["Turn off IP furnaces"] = TurnOffIPFurnaces;
            ProcessDictionary["Discard IP gases"] = DiscardIPGases;
            ProcessDictionary["Close IP"] = CloseIP;
            ProcessDictionary["Start collecting"] = StartCollecting;
            ProcessDictionary["Clear collection conditions"] = ClearCollectionConditions;
            ProcessDictionary["Collect until condition met"] = CollectUntilConditionMet;
            ProcessDictionary["Stop collecting"] = StopCollecting;
            ProcessDictionary["Stop collecting after bleed down"] = StopCollectingAfterBleedDown;
            ProcessDictionary["Evacuate and Freeze VTT"] = FreezeVtt;
            ProcessDictionary["Admit Dead CO2 into MC"] = AdmitDeadCO2;
            ProcessDictionary["Purify CO2 in MC"] = CleanupCO2InMC;
            ProcessDictionary["Discard MC gases"] = DiscardMCGases;
            ProcessDictionary["Divide sample into aliquots"] = DivideAliquots;
            Separators.Add(ProcessDictionary.Count);

            // Granular inlet port & sample process control
            ProcessDictionary["Turn on quartz furnace"] = TurnOnIpQuartzFurnace;
            ProcessDictionary["Turn off quartz furnace"] = TurnOffIpQuartzFurnace;
            ProcessDictionary["Turn on sample furnace"] = TurnOnIpSampleFurnace;
            ProcessDictionary["Adjust sample setpoint"] = AdjustIpSetpoint;
            ProcessDictionary["Wait for sample to rise to setpoint"] = WaitIpRiseToSetpoint;
            ProcessDictionary["Wait for sample to fall to setpoint"] = WaitIpFallToSetpoint;
            ProcessDictionary["Turn off sample furnace"] = TurnOffIpSampleFurnace;
            Separators.Add(ProcessDictionary.Count);

            // General-purpose process control actions
            ProcessDictionary["Wait for timer"] = WaitForTimer;
            ProcessDictionary["Wait for operator"] = WaitForOperator;
            Separators.Add(ProcessDictionary.Count);

            // Transferring CO2
            ProcessDictionary["Transfer CO2 from CT to VTT"] = TransferCO2FromCTToVTT;
            ProcessDictionary["Transfer CO2 from MC to VTT"] = TransferCO2FromMCToVTT;
            ProcessDictionary["Transfer CO2 from MC to GR"] = TransferCO2FromMCToGR;
            ProcessDictionary["Transfer CO2 from prior GR to MC"] = TransferCO2FromGRToMC;
            Separators.Add(ProcessDictionary.Count);

            // Flow control steps
            Separators.Add(ProcessDictionary.Count);

            // Utilities (generally not for sample processing)
            Separators.Add(ProcessDictionary.Count);
            ProcessDictionary["Exercise all Opened valves"] = ExerciseAllValves;
            ProcessDictionary["Close all Opened valves"] = CloseAllValves;
            ProcessDictionary["Exercise all LN Manifold valves"] = ExerciseLNValves;
            ProcessDictionary["Calibrate all multi-turn valves"] = CalibrateRS232Valves;
            ProcessDictionary["Measure MC volume (KV in MCP1)"] = MeasureVolumeMC;
            ProcessDictionary["Measure valve volumes (plug in MCP1)"] = MeasureValveVolumes;
            ProcessDictionary["Measure remaining chamber volumes"] = MeasureRemainingVolumes;
            ProcessDictionary["Check GR H2 density ratios"] = CalibrateGRH2;
            ProcessDictionary["Measure Extraction efficiency"] = MeasureExtractEfficiency;
            ProcessDictionary["Measure IP collection efficiency"] = MeasureIpCollectionEfficiency;

            // Test functions
            Separators.Add(ProcessDictionary.Count);
            ProcessDictionary["Test"] = Test;
            base.BuildProcessDictionary();
        }

        /// <summary>
        /// Opens the whole line to evacuation.
        /// TODO: Consider replacing this complex procedure with a simple one.
        /// </summary>
        protected override void OpenLine()
        {
            ProcessStep.Start("Open line");

            ProcessSubStep.Start("Close gas supplies");
            foreach (GasSupply g in GasSupplies.Values)
            {
                if (g.Destination.VacuumSystem == VacuumSystem1)
                    g.ShutOff();
            }

            // close gas flow valves after all shutoff valves are closed
            foreach (GasSupply g in GasSupplies.Values)
            {
                if (g.Destination.VacuumSystem == VacuumSystem1)
                    g.FlowValve?.CloseWait();
            }

            ProcessSubStep.End();

            VacuumSystem1.Evacuate(OkPressure);

            var gmWasOpened = GM.IsOpened && PreparedGRsAreOpened();
            var mcWasOpened = MC_Split.IsOpened && MC.Ports.All(p => p.IsOpened);
            var ctWasOpened = CT_VTT.IsOpened;
            var imWasOpened = IM.IsOpened;

            if (gmWasOpened && mcWasOpened && ctWasOpened && imWasOpened && IM_CT.IsOpened && VTT_MC.IsOpened)
            {
                VacuumSystem1.Evacuate();
                ProcessStep.End();
                return;
            }

            if (!mcWasOpened)
            {
                ProcessSubStep.Start($"Evacuate {MC_Split.Name}");
                VacuumSystem1.IsolateManifold();
                MC_Split.OpenAndEvacuateAll(OkPressure);        // include MC aliquot ports
                ProcessSubStep.End();
            }

            if (!gmWasOpened)
            {
                ProcessSubStep.Start($"Evacuate {GM.Name} and prepared GRs");
                VacuumSystem1.IsolateManifold();
                GM.Isolate();
                OpenPreparedGRs();
                GM.OpenAndEvacuate(OkPressure);
                ProcessSubStep.End();
            }
            else
            {
                GM.InternalValves.Open(); // ensure GM internal valves are open in case MC evacuation closed them.
            }

            if (!ctWasOpened)
            {
                ProcessSubStep.Start($"Evacuate {CT_VTT.Name}");
                VacuumSystem1.IsolateManifold();
                CT_VTT.OpenAndEvacuate(OkPressure);
                ProcessSubStep.End();
            }

            if (!imWasOpened)
            {
                ProcessSubStep.Start($"Evacuate {IM.Name}");
                VacuumSystem1.IsolateManifold();
                IM.OpenAndEvacuate(OkPressure);
                ProcessSubStep.End();
            }

            ProcessSubStep.Start($"Join and Evacuate all sections");
            OpenPreparedGRs();
            MC.PathToVacuum?.Open();     // Opens GM, too; avoid closing GR ports
            VTT.PathToVacuum?.Open();
            IM.PathToVacuum?.Open();
            IM_CT.Open();
            VTT_MC.Open();
            ProcessSubStep.End();

            ProcessStep.End();
        }


        /// <summary>
        /// Event handler for MC temperature and pressure changes
        /// </summary>
        protected override void UpdateSampleMeasurement(object sender = null, PropertyChangedEventArgs e = null)
        {
            var ugC = ugCinMC.Value;
            base.UpdateSampleMeasurement(sender, e);
            if (ugCinMC.Value != ugC)
                NotifyPropertyChanged(nameof(umolCinMC));
        }

        #region Process Control Parameters
        #endregion Process Control Parameters


        #region Process Control Properties
        #endregion Process Control Properties


        #region Process Steps
        #endregion Process Steps


        #endregion Process Management

        #region Test functions


        /// <summary>
        /// A faster OpenLine(); even better would be to use one Section, VS1All;
        /// </summary>
        protected virtual void FastOpenLine()
        {
            CloseAllGRs();
            VacuumSystem1.Isolate();
            IM.Open();
            GM.Open();

            IM_CT.Open();
            CT_VTT.Open();
            VTT_MC.Open();
            MC.OpenPorts();
            MC_GM.Open();
            MC.PathToVacuum?.Open();     // Opens GM, too
            VTT.PathToVacuum?.Open();
            IM.PathToVacuum?.Open();
            VacuumSystem1.Evacuate();
        }




        /// <summary>
        /// TODO: use a search function to make this system-independent
        /// and move it to the base class.
        /// </summary>
        protected void CalibrateManualHeaters()
        {
            var tc = Find<IThermocouple>("tCal");
            CalibrateManualHeater(Find<IHeater>("hIP1CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP2CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP3CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP4CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP5CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP6CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP7CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP8CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP9CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP10CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP11CCQ"), tc);
            CalibrateManualHeater(Find<IHeater>("hIP12CCQ"), tc);
        }


        protected virtual void TestGasSupplies()
        {
            //TestAdmit("O2.IML", IMO2Pressure);
            TestPressurize("CO2.MC", 75);
            //TestPressurize("CO2.MC", 1000);
            //TestPressurize("He.IMR", 800);
            //TestPressurize("He.MC", 80);
            //TestPressurize("He.GML", 800);
            //TestAdmit("He.GM", 760);
            //TestPressurize("H2.GMR", 100);
            //TestPressurize("H2.GMR", 900);
        }

        protected override void Test()
        {
       }

        #endregion Test functions

    }
}