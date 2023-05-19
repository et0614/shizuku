using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaCSharp
{
  public interface IVRFScheduller : IBACnetController
  {
    /// <summary>日時を同期させる</summary>
    void Synchronize();
  }
}
