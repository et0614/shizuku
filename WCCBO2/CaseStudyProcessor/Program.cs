using System;
using System.Diagnostics;
using System.Xml;

namespace CaseStudyProcessor
{
  internal class Program
  {

    /// <summary>Shizuku2のプロセス</summary>
    private static Process procS;

    /// <summary>ExcelControllerのプロセス</summary>
    private static Process procE;

    static void Main(string[] args)
    {
      killProcess("ExcelController");
      killProcess("Shizuku2");

      AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

      string[] files = Directory.GetFiles("schedules");
      for (int i = 0; i < files.Length; i++)
      {
        try
        {
          execOne(files[i]);
        }
        finally 
        {
          killProcess("ExcelController");
          killProcess("Shizuku2");
        }

        //5秒待機
        Thread.Sleep(5000);
      }
    }

    static void OnProcessExit(object sender, EventArgs e)
    {
      killProcess("ExcelController");
      killProcess("Shizuku2");
    }

    private static void killProcess(string procName)
    {
      Process[] processes = Process.GetProcessesByName(procName);
      foreach (Process process in processes)
      {
        try
        {
          process.Kill();
        }
        catch (Exception ex)
        {
          Console.WriteLine($"プロセス {process.ProcessName} (ID: {process.Id}) の終了中にエラーが発生しました: {ex.Message}");
        }
      }
    }

    private static void execOne(string file)
    {
      File.Copy(file, "schedule.xlsx", true);

      try
      {
        Console.WriteLine("Start Shizuku2.exe");

        procS = Process.Start(makeProcessStartInfo("Shizuku2.exe"));
        procS.OutputDataReceived += proc_OutputDataReceived;
        procS.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
        {
          Console.WriteLine("Shizuku2 Error: " + e.Data);
        };
        procS.BeginOutputReadLine();
        procS.BeginErrorReadLine();
      }
      catch
      {
        if (procS != null && !procS.HasExited) procS.Kill();
      }

      while (true)
      {
        if (procS != null && procS.HasExited)
        {
          Console.WriteLine("END");

          string fName = file.Substring(file.LastIndexOf('\\') + 1);
          string dirName = "data_" + fName.Remove(fName.Length - 5, 5);

          if (Directory.Exists(dirName))
            Directory.Delete(dirName, true);
          Directory.Move("data", dirName);
          return;
        }
      }
    }

    private static ProcessStartInfo makeProcessStartInfo(string exeName)
    {
      ProcessStartInfo procInfo = new ProcessStartInfo();
      procInfo.FileName = exeName;
      procInfo.CreateNoWindow = true; // コンソール・ウィンドウを開かない
      procInfo.UseShellExecute = false; // シェル機能を使用しない
      procInfo.RedirectStandardOutput = true; // 標準出力をリダイレクト
      procInfo.RedirectStandardInput = true; //標準入力をリダイレクト
      procInfo.RedirectStandardError = true;
      return procInfo;
    }

    private static void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
      Console.WriteLine("Shizuku2: " + e.Data);

      if (e.Data == null) return;

      //準備完了
      if (e.Data.StartsWith("Press \"Enter\" key to continue."))
      {
        try
        {
          //Excel controllerを準備
          Console.WriteLine("Start ExcelController.exe");
          procE = Process.Start(makeProcessStartInfo("ExcelController.exe"));
          procE.OutputDataReceived += proc_OutputDataReceived_E;
          procE.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
          {
            Console.WriteLine("ExcelController Error: " + e.Data);
          };
          procE.BeginOutputReadLine();
          procE.BeginErrorReadLine();
        }
        catch
        {
          if (procE != null && !procE.HasExited) procE.Kill();
        }
      }
      //計算終了
      else if (e.Data.StartsWith("Emulation finished. Press \"Enter\" key to exit."))
      {        
        procS.StandardInput.WriteLine((char)ConsoleKey.Enter);
        //ExcelControllerを閉じる
        if (procE != null && !procE.HasExited) procE.Kill();
        //if (procS != null && !procS.HasExited) procS.Kill();
      }
      //エラー終了
      else if (e.Data.StartsWith("Press any key to exit."))
      {
        procS.StandardInput.WriteLine((char)ConsoleKey.Enter);
        //ExcelControllerを閉じる
        if (procE != null && !procE.HasExited) procE.Kill();
        //if (procS != null && !procS.HasExited) procS.Kill();
      }
    }

    private static void proc_OutputDataReceived_E(object sender, DataReceivedEventArgs e)
    {
      Console.WriteLine("ExcelController: " + e.Data);

      if (e.Data == null) return;

      //Excel読み込み完了時
      if (e.Data.StartsWith("Loading excel data... done."))
      {
        procS.StandardInput.WriteLine((char)ConsoleKey.Enter);
      }
    }

  }
}