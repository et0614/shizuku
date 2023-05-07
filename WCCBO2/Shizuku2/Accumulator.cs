using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shizuku.Models
{
  /// <summary>積算器</summary>
  [Serializable]
  public class Accumulator: ImmutableAccumulator
  {

    #region インスタンス変数・プロパティ

    /// <summary>パルスレート以下の電力のあまり</summary>
    private double rest = 0;

    /// <summary>入力の時間単位[sec]</summary>
    private double seconds;

    /// <summary>積算値を取得する</summary>
    public double IntegratedValue { get; private set; }

    /// <summary>瞬時値を取得する</summary>
    public double InstantaneousValue { get; private set; }

    /// <summary>パルスレート[1/Pulse]を取得する</summary>
    public double PulseRate { get; private set; }

    /// <summary>精度(小数点以下桁数)を取得する</summary>
    public int Precision { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="seconds">入力の単位[sec]:/hの場合は3600,/minの場合は60</param>
    /// <param name="precision">精度(小数点以下桁数)</param>
    /// <param name="pulseRate">パルスレート[1/Pulse]</param>
    public Accumulator(int seconds, int precision, double pulseRate = 1)
    {
      this.seconds = seconds;
      PulseRate = pulseRate;
      Precision = precision;
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>検出値を更新する</summary>
    /// <param name="timeStep">計算時間間隔[sec]</param>
    /// <param name="input">入力</param>
    public void Update(double timeStep, double input)
    {
      InstantaneousValue = Math.Round(input, Precision);
      rest += InstantaneousValue * timeStep / seconds;
      IntegratedValue += PulseRate * (int)(rest / PulseRate);
      rest = rest % PulseRate;
    }

    /// <summary>積算値を0に戻す</summary>
    public void Reset() { IntegratedValue = 0; }

    #endregion

  }

  #region 読み取り専用インターフェース

  /// <summary>読み取り専用の積算器</summary>
  public interface ImmutableAccumulator
  {
    /// <summary>積算値を取得する</summary>
    double IntegratedValue { get; }

    /// <summary>瞬時値を取得する</summary>
    double InstantaneousValue { get; }

    /// <summary>パルスレート[1/Pulse]を取得する</summary>
    double PulseRate { get; }

    /// <summary>精度(小数点以下桁数)を取得する</summary>
    int Precision { get; }
  }

  #endregion

}
