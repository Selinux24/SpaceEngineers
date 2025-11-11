using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class Config
    {
        readonly Program program;
        readonly MyIni ini = new MyIni();
        string lastCustomData = null;

        public string LCDFilter { get; private set; } = "*";
        public BlockFilter<IMyTextPanel> TextPanelFilter { get; private set; }
        public BlockFilter<IMyCockpit> CockpitFilter { get; private set; }

        public Config(Program program)
        {
            this.program = program;
        }

        public void Load()
        {
            if (lastCustomData == program.Me.CustomData) return;

            MyIniParseResult result;
            if (!ini.TryParse(program.Me.CustomData, out result))
            {
                throw new Exception(result.ToString());
            }

            LCDFilter = ini.Get("LCD", "filter").ToString("*");
            TextPanelFilter = BlockFilter<IMyTextPanel>.Create(program.Me, LCDFilter);
            CockpitFilter = BlockFilter<IMyCockpit>.Create(program.Me, LCDFilter);

            if (program.Me.CustomData.Trim().Equals("")) Save();

            lastCustomData = program.Me.CustomData;
        }
        public void Save()
        {
            MyIniParseResult result;
            if (!ini.TryParse(program.Me.CustomData, out result))
            {
                throw new Exception(result.ToString());
            }
            ini.Set("LCD", "filter", LCDFilter);

            program.Me.CustomData = ini.ToString();
        }

        public void Reset()
        {
            program.Me.CustomData = "";
            Load();
            Save();
        }
    }
}
