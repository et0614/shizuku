namespace SampleVRFController
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
      tabControl1 = new TabControl();
      tabPage5 = new TabPage();
      btnOnOff = new Button();
      tabPage1 = new TabPage();
      label3 = new Label();
      label2 = new Label();
      lblSP = new Label();
      btnSPDown = new Button();
      btnSPUp = new Button();
      tabPage2 = new TabPage();
      lblMode = new Label();
      btnModeDown = new Button();
      btnModeUp = new Button();
      tabPage3 = new TabPage();
      lblAmount = new Label();
      btnAmountDown = new Button();
      btnAmountUp = new Button();
      tabPage4 = new TabPage();
      pbxDirection = new PictureBox();
      btnDirectionDown = new Button();
      btnDirectionUp = new Button();
      lb_iUnits = new ListBox();
      tabControl1.SuspendLayout();
      tabPage5.SuspendLayout();
      tabPage1.SuspendLayout();
      tabPage2.SuspendLayout();
      tabPage3.SuspendLayout();
      tabPage4.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)pbxDirection).BeginInit();
      SuspendLayout();
      // 
      // tabControl1
      // 
      tabControl1.Controls.Add(tabPage5);
      tabControl1.Controls.Add(tabPage1);
      tabControl1.Controls.Add(tabPage2);
      tabControl1.Controls.Add(tabPage3);
      tabControl1.Controls.Add(tabPage4);
      tabControl1.Dock = DockStyle.Fill;
      tabControl1.Location = new Point(250, 0);
      tabControl1.Name = "tabControl1";
      tabControl1.SelectedIndex = 0;
      tabControl1.Size = new Size(558, 729);
      tabControl1.TabIndex = 0;
      // 
      // tabPage5
      // 
      tabPage5.Controls.Add(btnOnOff);
      tabPage5.Location = new Point(8, 46);
      tabPage5.Name = "tabPage5";
      tabPage5.Padding = new Padding(3);
      tabPage5.Size = new Size(542, 675);
      tabPage5.TabIndex = 4;
      tabPage5.Text = "On/Off";
      tabPage5.UseVisualStyleBackColor = true;
      // 
      // btnOnOff
      // 
      btnOnOff.BackColor = Color.MistyRose;
      btnOnOff.Image = Resource.onoff;
      btnOnOff.Location = new Point(174, 214);
      btnOnOff.Name = "btnOnOff";
      btnOnOff.Size = new Size(210, 210);
      btnOnOff.TabIndex = 0;
      btnOnOff.UseVisualStyleBackColor = false;
      btnOnOff.Click += btnOnOff_Click;
      // 
      // tabPage1
      // 
      tabPage1.Controls.Add(label3);
      tabPage1.Controls.Add(label2);
      tabPage1.Controls.Add(lblSP);
      tabPage1.Controls.Add(btnSPDown);
      tabPage1.Controls.Add(btnSPUp);
      tabPage1.Location = new Point(8, 46);
      tabPage1.Name = "tabPage1";
      tabPage1.Padding = new Padding(3);
      tabPage1.Size = new Size(542, 675);
      tabPage1.TabIndex = 0;
      tabPage1.Text = "温度";
      tabPage1.UseVisualStyleBackColor = true;
      // 
      // label3
      // 
      label3.AutoSize = true;
      label3.Font = new Font("Yu Gothic UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
      label3.Location = new Point(373, 310);
      label3.Name = "label3";
      label3.Size = new Size(52, 45);
      label3.TabIndex = 2;
      label3.Text = "℃";
      // 
      // label2
      // 
      label2.AutoSize = true;
      label2.Font = new Font("Yu Gothic UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
      label2.Location = new Point(107, 288);
      label2.Name = "label2";
      label2.Size = new Size(84, 90);
      label2.TabIndex = 2;
      label2.Text = "設定\r\n温度";
      // 
      // lblSP
      // 
      lblSP.AutoSize = true;
      lblSP.Font = new Font("Yu Gothic UI", 31.875F, FontStyle.Regular, GraphicsUnit.Point);
      lblSP.Location = new Point(210, 270);
      lblSP.Name = "lblSP";
      lblSP.Size = new Size(140, 113);
      lblSP.TabIndex = 1;
      lblSP.Text = "28";
      // 
      // btnSPDown
      // 
      btnSPDown.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnSPDown.Location = new Point(200, 450);
      btnSPDown.Name = "btnSPDown";
      btnSPDown.Size = new Size(150, 100);
      btnSPDown.TabIndex = 0;
      btnSPDown.Text = "▽";
      btnSPDown.UseVisualStyleBackColor = true;
      btnSPDown.Click += btnSPUpDown_Click;
      // 
      // btnSPUp
      // 
      btnSPUp.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnSPUp.Location = new Point(200, 100);
      btnSPUp.Name = "btnSPUp";
      btnSPUp.Size = new Size(150, 100);
      btnSPUp.TabIndex = 0;
      btnSPUp.Text = "△";
      btnSPUp.UseVisualStyleBackColor = true;
      btnSPUp.Click += btnSPUpDown_Click;
      // 
      // tabPage2
      // 
      tabPage2.Controls.Add(lblMode);
      tabPage2.Controls.Add(btnModeDown);
      tabPage2.Controls.Add(btnModeUp);
      tabPage2.Location = new Point(8, 46);
      tabPage2.Name = "tabPage2";
      tabPage2.Padding = new Padding(3);
      tabPage2.Size = new Size(542, 675);
      tabPage2.TabIndex = 1;
      tabPage2.Text = "モード";
      tabPage2.UseVisualStyleBackColor = true;
      // 
      // lblMode
      // 
      lblMode.AutoSize = true;
      lblMode.Font = new Font("Yu Gothic UI", 31.875F, FontStyle.Regular, GraphicsUnit.Point);
      lblMode.Location = new Point(171, 283);
      lblMode.Name = "lblMode";
      lblMode.Size = new Size(218, 113);
      lblMode.TabIndex = 5;
      lblMode.Text = "冷房";
      // 
      // btnModeDown
      // 
      btnModeDown.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnModeDown.Location = new Point(205, 462);
      btnModeDown.Name = "btnModeDown";
      btnModeDown.Size = new Size(150, 100);
      btnModeDown.TabIndex = 3;
      btnModeDown.Text = "▽";
      btnModeDown.UseVisualStyleBackColor = true;
      btnModeDown.Click += btnModeUpDown_Click;
      // 
      // btnModeUp
      // 
      btnModeUp.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnModeUp.Location = new Point(205, 112);
      btnModeUp.Name = "btnModeUp";
      btnModeUp.Size = new Size(150, 100);
      btnModeUp.TabIndex = 4;
      btnModeUp.Text = "△";
      btnModeUp.UseVisualStyleBackColor = true;
      btnModeUp.Click += btnModeUpDown_Click;
      // 
      // tabPage3
      // 
      tabPage3.Controls.Add(lblAmount);
      tabPage3.Controls.Add(btnAmountDown);
      tabPage3.Controls.Add(btnAmountUp);
      tabPage3.Location = new Point(8, 46);
      tabPage3.Name = "tabPage3";
      tabPage3.Padding = new Padding(3);
      tabPage3.Size = new Size(542, 675);
      tabPage3.TabIndex = 2;
      tabPage3.Text = "風量";
      tabPage3.UseVisualStyleBackColor = true;
      // 
      // lblAmount
      // 
      lblAmount.Font = new Font("Yu Gothic UI", 31.875F, FontStyle.Regular, GraphicsUnit.Point);
      lblAmount.Location = new Point(119, 283);
      lblAmount.Name = "lblAmount";
      lblAmount.Size = new Size(300, 113);
      lblAmount.TabIndex = 8;
      lblAmount.Text = "強";
      lblAmount.TextAlign = ContentAlignment.MiddleCenter;
      // 
      // btnAmountDown
      // 
      btnAmountDown.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnAmountDown.Location = new Point(196, 462);
      btnAmountDown.Name = "btnAmountDown";
      btnAmountDown.Size = new Size(150, 100);
      btnAmountDown.TabIndex = 6;
      btnAmountDown.Text = "▽";
      btnAmountDown.UseVisualStyleBackColor = true;
      btnAmountDown.Click += btnAmountUpDown_Click;
      // 
      // btnAmountUp
      // 
      btnAmountUp.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnAmountUp.Location = new Point(196, 112);
      btnAmountUp.Name = "btnAmountUp";
      btnAmountUp.Size = new Size(150, 100);
      btnAmountUp.TabIndex = 7;
      btnAmountUp.Text = "△";
      btnAmountUp.UseVisualStyleBackColor = true;
      btnAmountUp.Click += btnAmountUpDown_Click;
      // 
      // tabPage4
      // 
      tabPage4.Controls.Add(pbxDirection);
      tabPage4.Controls.Add(btnDirectionDown);
      tabPage4.Controls.Add(btnDirectionUp);
      tabPage4.Location = new Point(8, 46);
      tabPage4.Name = "tabPage4";
      tabPage4.Padding = new Padding(3);
      tabPage4.Size = new Size(542, 675);
      tabPage4.TabIndex = 3;
      tabPage4.Text = "風向";
      tabPage4.UseVisualStyleBackColor = true;
      // 
      // pbxDirection
      // 
      pbxDirection.Image = Resource._0;
      pbxDirection.Location = new Point(187, 251);
      pbxDirection.Name = "pbxDirection";
      pbxDirection.Size = new Size(160, 160);
      pbxDirection.TabIndex = 12;
      pbxDirection.TabStop = false;
      // 
      // btnDirectionDown
      // 
      btnDirectionDown.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnDirectionDown.Location = new Point(196, 462);
      btnDirectionDown.Name = "btnDirectionDown";
      btnDirectionDown.Size = new Size(150, 100);
      btnDirectionDown.TabIndex = 9;
      btnDirectionDown.Text = "▽";
      btnDirectionDown.UseVisualStyleBackColor = true;
      btnDirectionDown.Click += btnDirectionUpDown_Click;
      // 
      // btnDirectionUp
      // 
      btnDirectionUp.Font = new Font("Yu Gothic UI", 19.875F, FontStyle.Regular, GraphicsUnit.Point);
      btnDirectionUp.Location = new Point(196, 112);
      btnDirectionUp.Name = "btnDirectionUp";
      btnDirectionUp.Size = new Size(150, 100);
      btnDirectionUp.TabIndex = 10;
      btnDirectionUp.Text = "△";
      btnDirectionUp.UseVisualStyleBackColor = true;
      btnDirectionUp.Click += btnDirectionUpDown_Click;
      // 
      // lb_iUnits
      // 
      lb_iUnits.Dock = DockStyle.Left;
      lb_iUnits.Font = new Font("Yu Gothic UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
      lb_iUnits.FormattingEnabled = true;
      lb_iUnits.ItemHeight = 45;
      lb_iUnits.Location = new Point(0, 0);
      lb_iUnits.Name = "lb_iUnits";
      lb_iUnits.SelectionMode = SelectionMode.MultiExtended;
      lb_iUnits.Size = new Size(250, 729);
      lb_iUnits.TabIndex = 1;
      lb_iUnits.SelectedIndexChanged += lb_iUnits_SelectedIndexChanged;
      // 
      // Form1
      // 
      AutoScaleDimensions = new SizeF(13F, 32F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(808, 729);
      Controls.Add(tabControl1);
      Controls.Add(lb_iUnits);
      FormBorderStyle = FormBorderStyle.FixedSingle;
      MaximizeBox = false;
      MinimizeBox = false;
      Name = "Form1";
      Text = "VRFコントローラサンプル";
      tabControl1.ResumeLayout(false);
      tabPage5.ResumeLayout(false);
      tabPage1.ResumeLayout(false);
      tabPage1.PerformLayout();
      tabPage2.ResumeLayout(false);
      tabPage2.PerformLayout();
      tabPage3.ResumeLayout(false);
      tabPage4.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)pbxDirection).EndInit();
      ResumeLayout(false);
    }

    #endregion

    private TabControl tabControl1;
    private TabPage tabPage1;
    private TabPage tabPage2;
    private TabPage tabPage3;
    private TabPage tabPage4;
    private Label lblSP;
    private Button btnSPDown;
    private Button btnSPUp;
    private ListBox lb_iUnits;
    private Label label3;
    private Label label2;
    private Label lblMode;
    private Button btnModeDown;
    private Button btnModeUp;
    private Label lblAmount;
    private Button btnAmountDown;
    private Button btnAmountUp;
    private Button btnDirectionDown;
    private Button btnDirectionUp;
    private PictureBox pbxDirection;
    private TabPage tabPage5;
    private Button btnOnOff;
  }
}