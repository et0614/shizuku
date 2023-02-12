using Popolo.Weather;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shizuku2
{
  internal class WeatherLoader
  {

    private readonly double[] dbTmp, hmdRatio, radiation;

    public WeatherLoader(uint seed, RandomWeather.Location location)
    {
      RandomWeather randomWeather = new RandomWeather(seed, location);
      //閏年にしておいて8784hにも対応させる
      randomWeather.MakeWeather(1, true, out dbTmp, out hmdRatio, out radiation, out bool[] _);
    }

    public void GetWeather(DateTime now, 
      out double drybulbTemperature, out double absoluteHumidity, 
      ref Sun sun)
    {
      int hoy1 = (int)(now - new DateTime(now.Year, 1, 1, 0, 0, 0)).TotalHours;
      int hoy2 = hoy1 == dbTmp.Length - 1 ? 0 : hoy1 + 1;
      double rt2 = (now.Minute * 60 + now.Second) / 3600d;
      double rt1 = 1.0 - rt2;

      drybulbTemperature = dbTmp[hoy1] * rt1 + dbTmp[hoy2] * rt2;
      absoluteHumidity = hmdRatio[hoy1] * rt1 + hmdRatio[hoy2] * rt2;
      double rad = radiation[hoy1] * rt1 + radiation[hoy2] * rt2;
      sun.SeparateGlobalHorizontalRadiation(rad, Sun.SeparationMethod.Erbs);
    }



  }
}
