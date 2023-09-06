using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rekindled.src
{
    interface ITransientTickable
    {
        void OnGameTick(float deltaTime);
    }
}
