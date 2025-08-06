using System;
using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class Navigator
    {
        readonly Config config;

        public Navigator(Config config)
        {
            this.config = config;
        }

        public void AproximateToDock(bool inGravity, Vector3D parking, string exchange, Vector3D fw, Vector3D up, List<Vector3D> wpList, Action onAproximationCompleted)
        {


            onAproximationCompleted?.Invoke();
        }

        public void LoadFromStorage(string storageLine)
        {
            var parts = storageLine.Split('¬');
            if (parts.Length != 6) return;

        }
        public string SaveToStorage()
        {
            List<string> parts = new List<string>()
            {
                
            };

            return string.Join("¬", parts);
        }
    }
}
