using Sandbox.ModAPI.Ingame;
using System.Collections.Generic;

namespace IngameScript
{
    class Drawing
    {
        readonly IMyTextSurface surface;
        readonly IMyTextSurfaceProvider provider;
        readonly List<SurfaceDrawing> surfaces = new List<SurfaceDrawing>();

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
            else if (block is IMyTextSurface)
            {
                surface = block as IMyTextSurface;
                surfaces.Add(new SurfaceDrawing(this, surface));
            }
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
