using Sandbox.ModAPI.Ingame;
using System;
using System.Linq;

namespace IngameScript
{
    class TextPanelDesc
    {
        public readonly IMyTerminalBlock Parent;
        public readonly IMyTextSurface TextSurface;

        public TextPanelDesc(IMyTerminalBlock parent, IMyTextSurface textSurface)
        {
            Parent = parent;
            TextSurface = textSurface;
        }

        public void Write(string log, string[] logLines)
        {
            TextSurface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;

            var blackList = Parent.CustomData.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (blackList.Length > 0)
            {
                string[] lines = logLines.Where(l => !blackList.Any(b => l.Contains(b))).ToArray();
                TextSurface.WriteText(string.Join(Environment.NewLine, lines));
            }
            else
            {
                TextSurface.WriteText(log, false);
            }
        }
    }
}
