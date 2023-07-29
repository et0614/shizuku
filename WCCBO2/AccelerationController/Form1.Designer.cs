namespace AccelerationController
{
  partial class Form1
  {
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      lbl_dateTime = new Label();
      tBar_acc = new TrackBar();
      lbl_acc = new Label();
      ((System.ComponentModel.ISupportInitialize)tBar_acc).BeginInit();
      SuspendLayout();
      // 
      // lbl_dateTime
      // 
      lbl_dateTime.AutoSize = true;
      lbl_dateTime.Font = new Font("Yu Gothic UI", 28.125F, FontStyle.Regular, GraphicsUnit.Point);
      lbl_dateTime.Location = new Point(24, 106);
      lbl_dateTime.Name = "lbl_dateTime";
      lbl_dateTime.Size = new Size(755, 100);
      lbl_dateTime.TabIndex = 0;
      lbl_dateTime.Text = "YYYY/MM/DD hh:mm";
      // 
      // tBar_acc
      // 
      tBar_acc.LargeChange = 60;
      tBar_acc.Location = new Point(21, 33);
      tBar_acc.Maximum = 600;
      tBar_acc.Minimum = 60;
      tBar_acc.Name = "tBar_acc";
      tBar_acc.Size = new Size(619, 90);
      tBar_acc.SmallChange = 60;
      tBar_acc.TabIndex = 1;
      tBar_acc.TickFrequency = 60;
      tBar_acc.Value = 120;
      tBar_acc.Scroll += tBar_acc_Scroll;
      // 
      // lbl_acc
      // 
      lbl_acc.AutoSize = true;
      lbl_acc.Font = new Font("Yu Gothic UI", 24F, FontStyle.Regular, GraphicsUnit.Point);
      lbl_acc.Location = new Point(653, 9);
      lbl_acc.Name = "lbl_acc";
      lbl_acc.Size = new Size(142, 86);
      lbl_acc.TabIndex = 2;
      lbl_acc.Text = "300";
      // 
      // Form1
      // 
      AutoScaleDimensions = new SizeF(13F, 32F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(807, 235);
      Controls.Add(lbl_acc);
      Controls.Add(tBar_acc);
      Controls.Add(lbl_dateTime);
      FormBorderStyle = FormBorderStyle.FixedSingle;
      MaximizeBox = false;
      MinimizeBox = false;
      Name = "Form1";
      ShowIcon = false;
      ((System.ComponentModel.ISupportInitialize)tBar_acc).EndInit();
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private Label lbl_dateTime;
    private TrackBar tBar_acc;
    private Label lbl_acc;
  }
}