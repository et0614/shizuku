using System;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shizuku2
{
  public interface IBACnetDevice
  {

    /// <summary>サービスを開始する</summary>
    void StartService();

    /// <summary>リソースを解放する</summary>
    void EndService();

    /// <summary>制御値を機器やセンサに反映する</summary>
    abstract void ApplyManipulatedVariables();

    /// <summary>機器やセンサの検出値を取得する</summary>
    abstract void ReadMeasuredValues();

  }
}
