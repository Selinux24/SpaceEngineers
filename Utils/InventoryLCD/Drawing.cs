using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript
{
    class Drawing
    {
        readonly IMyTextSurface surface;
        readonly IMyTextSurfaceProvider provider;
        readonly List<SurfaceDrawing> surfaces = new List<SurfaceDrawing>();

        public Dictionary<string, string> Symbol { get; private set; } = new Dictionary<string, string>();

        public Drawing(IMyTerminalBlock block)
        {
            if (block is IMyTextSurfaceProvider)
            {
                provider = block as IMyTextSurfaceProvider;
                for (int i = 0; i < provider.SurfaceCount; i++)
                {
                    var surface = provider.GetSurface(i);
                    surfaces.Add(new SurfaceDrawing(this, surface));
                }
            }
            else
            {
                surface = block as IMyTextSurface;
                surfaces.Add(new SurfaceDrawing(this, surface));
            }
            Initialize();
        }
        private void Initialize()
        {
            Symbol.Add("Cobalt", "Co");
            Symbol.Add("Nickel", "Ni");
            Symbol.Add("Magnesium", "Mg");
            Symbol.Add("Platinum", "Pt");
            Symbol.Add("Iron", "Fe");
            Symbol.Add("Gold", "Au");
            Symbol.Add("Silicon", "Si");
            Symbol.Add("Silver", "Ag");
            Symbol.Add("Stone", "Stone");
            Symbol.Add("Uranium", "U");
            Symbol.Add("Ice", "Ice");
        }

        public SurfaceDrawing GetSurfaceDrawing(int index = 0)
        {
            if (index < surfaces.Count)
            {
                return surfaces[index];
            }
            return null;
        }
        public void Dispose()
        {
            foreach (var surface in surfaces)
            {
                surface.Dispose();
            }
        }
        public void Clean()
        {
            foreach (var surface in surfaces)
            {
                surface.Clean();
            }
        }
    }
}
