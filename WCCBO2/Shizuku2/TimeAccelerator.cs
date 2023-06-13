using BaCSharp;
using PacketDotNet.Tcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shizuku2
{
  /// <summary>日時加速器</summary>
  internal class DateTimeAccelerator
  {

    #region インスタンス変数・プロパティ

    /// <summary>加速度</summary>
    private uint accRate;

    /// <summary>基準となる現実の日時</summary>
    private DateTime baseRealDateTime;

    /// <summary>基準となる加速された日時</summary>
    private DateTime baseDateTime;

    /// <summary>加速度を設定・取得する</summary>
    public uint AccelerationRate
    {
      get { return accRate; }
      set
      {
        if (accRate != value && 1 <= value)
        {
          baseDateTime = AcceleratedDateTime;
          baseRealDateTime = DateTime.Now;
          accRate = value; //順番大切。先にこれを変えてしまうとAcceleratedDateTime自体が変わる
        }
      }
    }

    /// <summary>加速された日時を設定・取得する</summary>
    public DateTime AcceleratedDateTime
    {
      get
      {
        return baseDateTime.AddSeconds
          ((DateTime.Now - baseRealDateTime).TotalSeconds * AccelerationRate);
      }
      set
      {
        baseDateTime = value;
        baseRealDateTime = DateTime.Now;
      }
    }

    /// <summary>基準となる現実の日時</summary>
    public DateTime BaseRealDateTime { get { return baseRealDateTime; } }

    /// <summary>基準となる加速された日時</summary>
    public DateTime BaseAcceleratedDateTime { get { return baseDateTime; } }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="accRate">加速度[-]</param>
    /// <param name="dTime">日時</param>
    public DateTimeAccelerator(uint accRate, DateTime dTime)
    {
      this.accRate = accRate;
      this.AcceleratedDateTime = dTime;
      this.baseRealDateTime = DateTime.Now;
    }

    #endregion

  }
}
