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
        string lCDFilter = "*";

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

            lCDFilter = ini.Get("LCD", "filter").ToString("*");
            TextPanelFilter = BlockFilter<IMyTextPanel>.Create(program.Me, lCDFilter);
            CockpitFilter = BlockFilter<IMyCockpit>.Create(program.Me, lCDFilter);

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
            ini.Set("LCD", "filter", lCDFilter);

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
