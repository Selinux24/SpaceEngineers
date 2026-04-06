using Sandbox.ModAPI.Ingame;
using System;
using System.Linq;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    class TextPanelDesc
    {
        const string SectionInfo = "Info";

        readonly IMyTerminalBlock block;
        readonly IMyTextSurface textSurface;
        readonly MyIni ini = new MyIni();

        string lastCustomData = null;
        bool showShips = true;
        bool showExchanges = true;
        bool showExchangeRequests = true;
        bool showFlightPlans = true;

        public TextPanelDesc(IMyTerminalBlock block, IMyTextSurface textSurface, bool useCustomData)
        {
            this.block = block;
            this.textSurface = textSurface;

            if (useCustomData && string.IsNullOrEmpty(block.CustomData))
            {
                WriteDefaultCustomData();
            }
        }

        public void WriteLog(string log, string[] logLines)
        {
            Update();

            textSurface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;

            var blackList = lastCustomData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (blackList.Length > 0)
            {
                string[] lines = logLines.Where(l => !blackList.Any(b => l.Contains(b))).ToArray();
                textSurface.WriteText(string.Join(Environment.NewLine, lines));
            }
            else
            {
                textSurface.WriteText(log, false);
            }
        }
        public void WriteData(string title, string exchanges, string ships, string requests, string flightPlans)
        {
            Update();

            textSurface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;

            textSurface.WriteText(title, false);
            if (showExchanges) textSurface.WriteText(exchanges, true);
            if (showShips) textSurface.WriteText(ships, true);
            if (showExchangeRequests) textSurface.WriteText(requests, true);
            if (showFlightPlans) textSurface.WriteText(flightPlans, true);
        }
        void Update()
        {
            bool changed = block.CustomData != lastCustomData;
            if (!changed) return;
            lastCustomData = block.CustomData;

            MyIniParseResult result;
            ini.TryParse(lastCustomData, out result);

            showShips = ini.Get(SectionInfo, "ShowShips").ToBoolean(true);
            showExchanges = ini.Get(SectionInfo, "ShowExchanges").ToBoolean(true);
            showExchangeRequests = ini.Get(SectionInfo, "ShowExchangeRequests").ToBoolean(true);
            showFlightPlans = ini.Get(SectionInfo, "ShowFlightPlans").ToBoolean(true);
        }
        void WriteDefaultCustomData()
        {
            ini.Set(SectionInfo, "ShowShips", true);
            ini.Set(SectionInfo, "ShowExchanges", true);
            ini.Set(SectionInfo, "ShowExchangeRequests", true);
            ini.Set(SectionInfo, "ShowFlightPlans", true);
            block.CustomData = ini.ToString();
        }
    }
}
