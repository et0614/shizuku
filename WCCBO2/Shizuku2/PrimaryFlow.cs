using Popolo.Numerics;

namespace Shizuku2
{
  public static class PrimaryFlow
  {

    #region 定数宣言

    /// <summary>ルンゲクッタ法の刻み幅</summary>
    /// <remarks>最低でも0.01くらいじゃないと重い</remarks>
    private const double DELTA = 0.01;

    /// <summary>重力加速度[kg/s2]</summary>
    private const double GRAV = 9.8;

    /// <summary>熱と運動量の拡散に関する定数</summary>
    /// <remarks>
    /// 戸河里モデルではASHRAE論文よりλ=0.65を使っている
    /// 窪田は暫定値としてλ=0.75を採用
    /// </remarks>
    private const double LAMBDA = 0.65;

    /// <summary>吹き出し定数[-]</summary>
    private const double KP = 4.0;

    /// <summary>ストロット幅[m]</summary>
    /// <remarks>4方向吹き出しだと0.5くらいか</remarks>
    private const double SLOT_WIDTH = 0.5;

    /// <summary>天井高[m]</summary>
    private const double CEILING_HEIGHT = 2.7;

    /// <summary>下方空間高[m]</summary>
    private const double LOWER_AREA_HEIGHT = 1.7;

    /// <summary>首の位置</summary>
    private const double NECK_HEIGHT = 1.0;

    /// <summary>ドラフトの発生する最小風量[m/s]</summary>
    private const double DRAFT_VEL_LIMIT = 0.05;

    #endregion

    #region スロット吹き出しの噴流計算

    /// <summary>下方への吹き出し結果を計算する</summary>
    /// <param name="supplyTemperature">給気温度[C]</param>
    /// <param name="ambientTemperature">周囲温度[C]</param>
    /// <param name="velocity">吹き出し風速[m/s]</param>
    /// <param name="dtdy">温度勾配[K/m]</param>
    /// <param name="direction">吹き出し角度[radian]（水平から下向きに）</param>
    /// <param name="lowHRate">下部空間に吹き出す熱量比[-]</param>
    /// <param name="velocityAtNeck">首高さでの風速[m/s]</param>
    /// <param name="tempAtNeck">首高さでの温度[C]</param>
    public static void CalcBlowDown(
      double supplyTemperature, double ambientTemperature, double velocity, double dtdy, double direction,
      out double lowHRate, out double velocityAtNeck, out double tempAtNeck, out double jetLengthAtNeck)
    {
      calcBlowDown
        (false, supplyTemperature, ambientTemperature, velocity, dtdy, direction, out lowHRate, out velocityAtNeck, out tempAtNeck, out jetLengthAtNeck, out _, out _);
    }

    /// <summary>下方への吹き出し結果を計算する</summary>
    /// <param name="supplyTemperature">給気温度[C]</param>
    /// <param name="ambientTemperature">周囲温度[C]</param>
    /// <param name="velocity">吹き出し風速[m/s]</param>
    /// <param name="dtdy">温度勾配[K/m]</param>
    /// <param name="direction">吹き出し角度[radian]（水平から下向きに）</param>
    /// <param name="lowHRate">下部空間に吹き出す熱量比[-]</param>
    /// <param name="velocityAtNeck">首高さでの風速[m/s]</param>
    /// <param name="tempAtNeck">首高さでの温度[C]</param>
    /// <param name="xPath">X軸軌跡</param>
    /// <param name="yPath">Y軸軌跡</param>
    public static void CalcBlowDown(
      double supplyTemperature, double ambientTemperature, double velocity, double dtdy, double direction,
      out double lowHRate, out double velocityAtNeck, out double tempAtNeck, out double jetLengthAtNeck, out double[] xPath, out double[] yPath)
    {
      calcBlowDown
        (true, supplyTemperature, ambientTemperature, velocity, dtdy, direction, out lowHRate, out velocityAtNeck, out tempAtNeck, out jetLengthAtNeck, out xPath, out yPath);
    }

    /// <summary>下方への吹き出し結果を計算する</summary>
    /// <param name="needPath">XYの軌跡を出力するか否か</param>
    /// <param name="supplyTemperature">給気温度[C]</param>
    /// <param name="ambientTemperature">周囲温度[C]</param>
    /// <param name="velocity">吹き出し風速[m/s]</param>
    /// <param name="dtdy">温度勾配[K/m]</param>
    /// <param name="direction">吹き出し角度[radian]（水平から下向きに）</param>
    /// <param name="lowHRate">下部空間に吹き出す熱量比[-]</param>
    /// <param name="velocityAtNeck">首高さでの風速[m/s]</param>
    /// <param name="tempAtNeck">首高さでの温度[C]</param>
    /// <param name="xPath">X軸軌跡</param>
    /// <param name="yPath">Y軸軌跡</param>
    private static void calcBlowDown(
      bool needPath, double supplyTemperature, double ambientTemperature, double velocity, double dtdy, double direction,
      out double lowHRate, out double velocityAtNeck, out double tempAtNeck, out double jetLengthAtNeck,
      out double[] xPath, out double[] yPath)
    {
      //温度差が小さすぎると0割で発散する
      double dt = supplyTemperature - ambientTemperature;
      if (Math.Abs(dt) < 0.1) supplyTemperature = ambientTemperature + Math.Max(1, Math.Sign(dt)) * 0.1;

      //入力範囲を調整
      direction = Math.Max(5d / 180 * Math.PI, Math.Min(85d / 180d * Math.PI, direction));　//5-85度
      velocity = Math.Max(0, Math.Min(4.0, velocity)); //0-4m/s
      dtdy = Math.Max(0, Math.Min(5, dtdy));

      if (velocity == 0)
      {
        lowHRate = velocityAtNeck = tempAtNeck = jetLengthAtNeck = 0;
        xPath = new double[0];
        yPath = new double[0];
        return;
      }

      bool isCoolingMode = supplyTemperature < ambientTemperature;
      List<double> xpList, ypList;
      if (needPath)
      {
        xpList = new List<double>();
        ypList = new List<double>();
        xpList.Add(0);
        ypList.Add(0);
      }
      else xpList = ypList = null;
      lowHRate = 0;
      velocityAtNeck = tempAtNeck = jetLengthAtNeck = 0;

      //無次元数・係数を用意
      double cos0_15 = Math.Pow(Math.Cos(direction), 1.5);
      double cos0_05 = Math.Sqrt(Math.Cos(direction));
      double arN = getArNumber(supplyTemperature, ambientTemperature, SLOT_WIDTH, velocity);
      double alpha = getAlpha(supplyTemperature, ambientTemperature, velocity, dtdy);
      double cfXYS = Math.Pow(LAMBDA * KP / ((1 + LAMBDA) * arN * arN), 1d / 3) * SLOT_WIDTH;
      double cfU = Math.Pow((1 + LAMBDA) / LAMBDA * KP * arN, 1d / 3) * velocity;
      //double cfT = Math.Sqrt((1 + LAMBDA) / LAMBDA) * Math.Pow(LAMBDA / (1 + LAMBDA) * KP * arN, 1d / 3) * (ambientTemperature - supplyTemperature);
      double cfT = Math.Sqrt((1 + LAMBDA) / 2) * Math.Pow(LAMBDA / (1 + LAMBDA) * KP * arN, 1d / 3) * (ambientTemperature - supplyTemperature);

      //誤差関数の定義
      ODESolver.DifferentialEquations deqX = delegate (double x, double[] yx, ref double[] dyx)
      {
        double bf = 1 + yx[2] * yx[2];
        dyx[0] = 1;
        dyx[1] = yx[2];
        dyx[2] = Math.Pow(bf, 0.25) * Math.Sqrt(yx[3]) * yx[4] / cos0_15;
        dyx[3] = Math.Sqrt(bf);
        dyx[4] = -alpha * cos0_05 * Math.Pow(bf, 0.25) * yx[2] * Math.Sqrt(yx[3]);
      };

      ODESolver.DifferentialEquations deqY = delegate (double y, double[] xy, ref double[] dxy)
      {
        double bf = 1 + xy[2] * xy[2];
        dxy[0] = xy[2];
        dxy[1] = 1;
        dxy[2] = -Math.Pow(bf, 0.25) * Math.Pow(xy[2], 2.5) * Math.Sqrt(xy[3]) * xy[4] / cos0_15;
        dxy[3] = Math.Sqrt(bf);
        dxy[4] = -alpha * cos0_05 * Math.Pow(bf, 0.25) * Math.Sqrt(xy[3] / xy[2]);
      };

      //無次元状態変数：X, Y, dY/dX, S, 1
      double dYdX = Math.Tan(direction);
      double[] yy0 = new double[] { 0, 0, isCoolingMode ? dYdX : -dYdX, 0, 1 };
      double[] yy1 = new double[5];

      bool dyMode = false;
      double um1 = 0; //1時点前のu
      double tm1 = 0; //1時点前のt
      double sm1 = 0; //1時点前のs
      double ym1 = 0; //1時点前のy
      double ym2 = 0; //2時点前のy
      double htUp = 0; //上部熱量
      double htLw = 0; //下部熱量
      double utsm1 = 1.0;
      while (true)
      {
        //傾きが浅ければdy/dx、急ならばdx/dy
        if (1.0 < yy0[2])
        {
          dyMode = !dyMode;
          yy0[2] = 1 / yy0[2];
        }

        //微分方程式を解く
        if (dyMode) ODESolver.SolveRKGill(deqY, DELTA, yy0[1], yy0, ref yy1);
        else ODESolver.SolveRKGill(deqX, DELTA, yy0[0], yy0, ref yy1);
        for (int j = 0; j < 5; j++) yy0[j] = yy1[j];

        //無次元数を解凍
        double x = cfXYS * yy0[0];
        double y = cfXYS * (isCoolingMode ? -yy0[1] : yy0[1]); //浮力の方向による調整
        double s = cfXYS * yy0[3];
        double dmU = (dyMode ?
          cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) / Math.Sqrt(yy0[2] * yy0[3]) :
          cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) / Math.Sqrt(yy0[3]));
        double u = cfU * dmU;
        double te = ambientTemperature + y * dtdy;
        double dmT = (dyMode ?
          yy0[4] / (cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) * Math.Sqrt(yy0[3] / yy0[2])) :
          yy0[4] / (cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) * Math.Sqrt(yy0[3])));
        //double t = te - cfT * (isCoolingMode ? dmT : -dmT);
        double t = te - cfT * dmT; //debug
        double uts = dmU * dmT * yy0[3];
        double ht = uts - utsm1;
        utsm1 = uts;
        //double ht = u * s * (y - ym1) * dtdy;
        if (needPath)
        {
          xpList.Add(x);
          ypList.Add(y);
        }

        //首位置での温度と速度を出力
        if (-(CEILING_HEIGHT - NECK_HEIGHT) < ym1 && y < -(CEILING_HEIGHT - NECK_HEIGHT))
        {
          double pR = (y + (CEILING_HEIGHT - NECK_HEIGHT)) / (y - ym1);
          double cR = 1.0 - pR;
          double tem1 = ambientTemperature + ym1 * dtdy;

          jetLengthAtNeck = s * cR + sm1 * pR;
          //スロー定数を使って計算するため、吹き出し口直近の第1域、2域では異常な値が出る。以下はその回避処理
          double vlAve = u * cR + um1 * pR;
          double teAve = te * cR + tem1 * pR;
          double tAve = t * cR + tm1 * pR;
          velocityAtNeck = vlAve;
          //velocityAtNeck = Math.Min(velocity, vlAve); //吹き出し風速は超えない←この措置をとると、吹き出しが強くてすぐに到達する場合に変なボーナスになるからコメントアウト
          if (isCoolingMode) tempAtNeck = Math.Max(supplyTemperature, Math.Min(teAve, tAve));
          else tempAtNeck = Math.Min(supplyTemperature, Math.Max(teAve, tAve));
        }

        //熱量を上下空間に配分
        if (-(CEILING_HEIGHT - LOWER_AREA_HEIGHT) < y)
        {
          if (ym1 < -(CEILING_HEIGHT - LOWER_AREA_HEIGHT))
          {
            double lwRate = (-(CEILING_HEIGHT - LOWER_AREA_HEIGHT) - ym1) / (y - ym1);
            htUp += (1 - lwRate) * ht;
            htLw += lwRate * ht;
          }
          else htUp += ht;
        }
        else
        {
          if (-(CEILING_HEIGHT - LOWER_AREA_HEIGHT) < ym1)
          {
            double upRate = (-(CEILING_HEIGHT - LOWER_AREA_HEIGHT) - ym1) / (y - ym1);
            htUp += upRate * ht;
            htLw += (1 - upRate) * ht;
          }
          else htLw += ht;
        }

        //計算打ち切り判定
        if (
          (u <= 0.25) ||                              //0.25m/s以下（第4域）に到達
          (isCoolingMode && y > ym1 && ym2 > ym1) ||  //冷却時に極小値
          (!isCoolingMode && y < ym1 && ym2 < ym1) || //加熱時に極大値（1）
          (!isCoolingMode && ym1 < y) || //加熱時に反転（下向きに吹き出す場合は1の前にこれが発生するので1は意味がないが・・・）
          (0 < y) ||                                  //天井にあたった場合
          (y < -CEILING_HEIGHT)                       //床にあたった場合
          )
          break;

        ym2 = ym1;
        ym1 = y;
        sm1 = s;
        tm1 = t;
        um1 = u;
      }

      if (htLw == 0) lowHRate = 0;
      else
      {
        double htSum = htUp + htLw;
        if (Math.Abs(htSum) < 1e-5) lowHRate = 0; //極めて交換熱量が小さい場合の対応
        else lowHRate = htLw / htSum;
      }
      if (needPath)
      {
        xPath = xpList.ToArray();
        yPath = ypList.ToArray();
      }
      else
        xPath = yPath = null;
    }

    /// <summary>吹き出し後の気流を追跡する</summary>
    /// <param name="step">計算ステップ数</param>
    /// <param name="kp">吹き出し定数[-]（スロット吹き出しの場合は4-5くらい）</param>
    /// <param name="supplyTemperature">給気温度[C]</param>
    /// <param name="ambientTemperature">周囲温度[C]</param>
    /// <param name="slotWidth">スロット幅[m]</param>
    /// <param name="velocity">吹き出し風速[m/s]</param>
    /// <param name="dtdy">温度勾配[K/m]</param>
    /// <param name="direction">吹き出し角度[radian]（水平から下向きに）</param>
    /// <param name="x">X座標[m]</param>
    /// <param name="y">Y座標[m]</param>
    /// <param name="s">軌跡距離[m]</param>
    /// <param name="u">速度[m/s]</param>
    /// <param name="t">温度[C]</param>
    public static void SimulateSlotType(
      int step, double kp, double supplyTemperature, double ambientTemperature, double slotWidth, double velocity, double dtdy, double direction,
      out double[] x, out double[] y, out double[] s, out double[] u, out double[] t, out double[] ht)
    {
      //出力変数を用意
      x = new double[step + 1];
      y = new double[step + 1];
      s = new double[step + 1];
      u = new double[step + 1];
      t = new double[step + 1];
      ht = new double[step + 1];
      x[0] = y[0] = s[0] = 0;
      u[0] = velocity;
      t[0] = supplyTemperature;

      double alpha = getAlpha(supplyTemperature, ambientTemperature, velocity, dtdy);
      double arN = getArNumber(supplyTemperature, ambientTemperature, slotWidth, velocity);

      double[] ndX, ndY, ndS, ndU, ndT;
      SimulateSlotType_NoDimension(
        alpha, step, Math.Tan(direction), supplyTemperature < ambientTemperature,
        out ndX, out ndY, out ndS, out ndU, out ndT);

      double cfXYS = Math.Pow(LAMBDA * KP / ((1 + LAMBDA) * arN * arN), 1d / 3) * slotWidth;
      double cfU = Math.Pow((1 + LAMBDA) / LAMBDA * KP * arN, 1d / 3) * velocity;
      double cfT = Math.Sqrt((1 + LAMBDA) / LAMBDA) * Math.Pow(LAMBDA / (1 + LAMBDA) * KP * arN, 1d / 3) * (ambientTemperature - supplyTemperature);

      for (int i = 0; i < ndX.Length; i++)
      {
        x[i + 1] = cfXYS * ndX[i];
        y[i + 1] = cfXYS * ndY[i];
        s[i + 1] = cfXYS * ndS[i];
        u[i + 1] = cfU * ndU[i];
        double te = ambientTemperature + y[i + 1] * dtdy;
        t[i + 1] = te - cfT * ndT[i];
        ht[i + 1] = Math.Sqrt((1 + LAMBDA) / LAMBDA) * u[i + 1] * s[i + 1] * ((y[i + 1] - y[i]) * dtdy / (s[i + 1] - s[i]));
      }
    }

    /// <summary>吹き出し後の気流を追跡する</summary>
    /// <param name="alpha">無次元係数</param>
    /// <param name="maxStep">計算ステップ数</param>
    /// <param name="dydx">吹き出し角度[m/m]</param>
    /// <param name="isChilledAir">冷気吹き出しか否か</param>
    /// <param name="ndX">無次元X座標[-]</param>
    /// <param name="ndY">無次元Y座標[-]</param>
    /// <param name="ndS">無次元軌跡距離[-]</param>
    /// <param name="ndU">無次元速度[-]</param>
    /// <param name="ndT">無次元温度差[-]</param>
    public static void SimulateSlotType_NoDimension(
      double alpha, int maxStep, double dydx, bool isChilledAir,
      out double[] ndX, out double[] ndY, out double[] ndS, out double[] ndU, out double[] ndT)
    {
      //ルンゲクッタの刻み幅
      const double DELTA = 0.05;

      if (!isChilledAir) dydx = -dydx; //浮力が作用する向きを+にとる
      double cos0 = Math.Cos(Math.Atan(dydx));
      double cos0_15 = Math.Pow(cos0, 1.5);
      double cos0_05 = Math.Sqrt(cos0);

      //出力変数を用意
      List<double> xLs = new List<double>();
      List<double> yLs = new List<double>();
      List<double> sLs = new List<double>();
      List<double> uLs = new List<double>();
      List<double> tLs = new List<double>();

      ODESolver.DifferentialEquations deqX = delegate (double x, double[] yx, ref double[] dyx)
      {
        double bf = 1 + yx[2] * yx[2];
        dyx[0] = 1;
        dyx[1] = yx[2];
        dyx[2] = Math.Pow(bf, 0.25) * Math.Sqrt(yx[3]) * yx[4] / cos0_15;
        dyx[3] = Math.Sqrt(bf);
        dyx[4] = -alpha * cos0_05 * Math.Pow(bf, 0.25) * yx[2] * Math.Sqrt(yx[3]);
      };

      ODESolver.DifferentialEquations deqY = delegate (double y, double[] xy, ref double[] dxy)
      {
        double bf = 1 + xy[2] * xy[2];
        dxy[0] = xy[2];
        dxy[1] = 1;
        dxy[2] = -Math.Pow(bf, 0.25) * Math.Pow(xy[2], 2.5) * Math.Sqrt(xy[3]) * xy[4] / cos0_15;
        dxy[3] = Math.Sqrt(bf);
        dxy[4] = -alpha * cos0_05 * Math.Pow(bf, 0.25) * Math.Sqrt(xy[3] / xy[2]);
      };

      //状態変数：X, Y, dY/dX, S, 1
      double[] yy0 = new double[] { 0, 0, dydx, 0, 1 };
      double[] yy1 = new double[5];

      bool dyMode = false;
      for (int i = 0; i < maxStep; i++)
      {
        //if (1.0 < yy0[2] || yy0[2] < -1.0) //理屈からはこちらの方が安定するように思うのだが・・・発散しやすい
        if (1.0 < yy0[2])
        {
          dyMode = !dyMode;
          yy0[2] = 1 / yy0[2];
        }

        if (dyMode) ODESolver.SolveRKGill(deqY, DELTA, yy0[1], yy0, ref yy1);
        else ODESolver.SolveRKGill(deqX, DELTA, yy0[0], yy0, ref yy1);
        for (int j = 0; j < 5; j++) yy0[j] = yy1[j];

        xLs.Add(yy0[0]);
        yLs.Add(isChilledAir ? -yy0[1] : yy0[1]); //浮力の方向による調整
        sLs.Add(yy0[3]);
        uLs.Add(dyMode ?
          cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) / Math.Sqrt(yy0[2] * yy0[3]) :
          cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) / Math.Sqrt(yy0[3]));
        tLs.Add(dyMode ?
          yy0[4] / (cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) * Math.Sqrt(yy0[3] / yy0[2])) :
          yy0[4] / (cos0_05 * Math.Pow(1 + yy0[2] * yy0[2], 0.25) * Math.Sqrt(cos0_05 * yy0[3])));

        //冷却中にY極小値・加熱中にY極大値を記録した場合には計算打ち切り
        if (1 < i && (
          (isChilledAir && yLs[i] > yLs[i - 1] && yLs[i - 2] > yLs[i - 1]) ||
          (!isChilledAir && yLs[i] < yLs[i - 1] && yLs[i - 2] < yLs[i - 1])))
          break;
      }

      ndX = xLs.ToArray();
      ndY = yLs.ToArray();
      ndS = sLs.ToArray();
      ndU = uLs.ToArray();
      ndT = tLs.ToArray();
    }

    #endregion

    #region ドラフトリスクの評価

    /// <summary>局所気流による不満足者率[-]を計算する</summary>
    /// <param name="localTemp">局所温度[C]</param>
    /// <param name="localVelocity">局所風速[m/s]</param>
    /// <param name="turbulance">局所気流の乱れの強さ[-]</param>
    /// <returns>局所気流による不満足者率[-]</returns>
    public static double getLocalDraftRate(double localTemp, double localVelocity, double turbulance = 0.4)
    {
      if (localVelocity < DRAFT_VEL_LIMIT) return 0;

      localTemp = Math.Max(20, Math.Min(34, localTemp)); //ここ、ISO7730と違う
      turbulance = Math.Max(0.1, Math.Min(0.6, turbulance));
      double drft = (34 - localTemp) * Math.Pow(localVelocity - 0.05, 0.62) * (0.37 * localVelocity * turbulance + 0.0314);
      return Math.Max(0, Math.Min(1.0, drft));
    }

    public static double GetDraftRate(
      double centerTemp, double centerVelocity, double ambientTemp, double jetLength, double divNumber = 10)
    {
      //距離が0以下の場合には不満なし（首元まで到達しない）
      if (jetLength <= 0) return 0;

      double r_dr = jetLength / KP * Math.Sqrt(2d / Math.PI * Math.Log(centerVelocity / DRAFT_VEL_LIMIT));

      double bf = -Math.PI / 2d * Math.Pow(KP / jetLength, 2);
      double r2_pv = 0;
      double drw = 0;
      double delta_r = r_dr / divNumber;
      for (int i = 0; i < divNumber - 1; i++)
      {
        double r_an = (i + 0.5) * delta_r;
        double r2 = r_an * r_an;
        double a_an = r2 - r2_pv;
        r2_pv = r2;
        double u_an = centerVelocity * Math.Exp(bf * r2);
        double t_an = ambientTemp - (ambientTemp - centerTemp) * Math.Exp(bf * LAMBDA * r2);
        drw += a_an * getLocalDraftRate(t_an, u_an);
      }
      return drw / (r_dr * r_dr);
    }

    #endregion

    #region 補助関数

    /// <summary>アルキメデス数[-]を計算する</summary>
    /// <param name="supplyTemperature">給気温度[C]</param>
    /// <param name="ambientTemperature">周囲温度[C]</param>
    /// <param name="slotWidth">スロット幅[m]</param>
    /// <param name="velocity">吹き出し風速[m/s]</param>
    /// <returns>アルキメデス数[-]</returns>
    private static double getArNumber
      (double supplyTemperature, double ambientTemperature, double slotWidth, double velocity)
    {
      double dt = Math.Abs(ambientTemperature - supplyTemperature);
      double beta = 1.0 / (0.5 * (supplyTemperature + ambientTemperature) + 273.15);
      return GRAV * beta * dt * slotWidth / Math.Pow(velocity, 2);
    }

    /// <summary></summary>
    /// <param name="supplyTemperature">給気温度[C]</param>
    /// <param name="ambientTemperature">周囲温度[C]</param>
    /// <param name="velocity">吹き出し風速[m/s]</param>
    /// <param name="dtdy">温度勾配[K/m]</param>
    /// <returns></returns>
    private static double getAlpha
      (double supplyTemperature, double ambientTemperature, double velocity, double dtdy)
    {
      double dt = Math.Abs(ambientTemperature - supplyTemperature);
      double beta = 1.0 / (0.5 * (supplyTemperature + ambientTemperature) + 273.15);

      return Math.Sqrt(2.0 / (1.0 + LAMBDA) / GRAV / beta * Math.Pow(velocity / dt, 2) * dtdy);
    }

    #endregion

  }
}
