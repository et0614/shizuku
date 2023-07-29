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

    /// <summary>加速度[sec/sec]</summary>
    private int accRate;

    /// <summary>加速度を設定・取得する</summary>
    public int AccelerationRate
    {
      get { return accRate; }
      set
      {
        InitDateTime(value, AcceleratedDateTime);
      }
    }

    /// <summary>加速された日時を取得する</summary>
    public DateTime AcceleratedDateTime
    {
      get
      {
        return BaseAcceleratedDateTime.AddSeconds
          ((DateTime.Now - BaseRealDateTime).TotalSeconds * AccelerationRate);
      }
    }

    /// <summary>加速が開始された現実の日時を設定・取得する</summary>
    public DateTime BaseRealDateTime { set; get; }

    /// <summary>加速された日時の加速開始日時を設定・取得する</summary>
    public DateTime BaseAcceleratedDateTime { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="accRate">加速度[-]</param>
    /// <param name="dTime">日時</param>
    public DateTimeAccelerator(int accRate, DateTime dTime)
    {
      this.accRate = accRate;
      BaseAcceleratedDateTime = dTime;
      BaseRealDateTime = DateTime.Now;
    }

    #endregion

    #region インスタンスメソッド

    public void InitDateTime
      (int accRate, DateTime acceleratedDateTime)
    {
      if (0 <= accRate)
      {
        BaseAcceleratedDateTime = acceleratedDateTime;
        BaseRealDateTime = DateTime.Now;
        this.accRate = accRate; //順番大切。先にこれを変えてしまうとAcceleratedDateTime自体が変わる
      }
    }

    public void InitDateTime
     (int accRate, DateTime baseRealDateTime, DateTime baseAcceleratedDateTime)
    {
      if (0 <= accRate)
      {
        BaseAcceleratedDateTime = baseAcceleratedDateTime;
        BaseRealDateTime = baseRealDateTime;
        this.accRate = accRate;
      }
    }

    #endregion

  }
}
