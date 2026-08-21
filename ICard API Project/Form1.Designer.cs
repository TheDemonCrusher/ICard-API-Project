namespace ICard_API_Project
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
            startBtn = new Button();
            stopBtn = new Button();
            importBtn = new Button();
            SuspendLayout();
            // 
            // startBtn
            // 
            startBtn.Location = new Point(82, 82);
            startBtn.Margin = new Padding(3, 2, 3, 2);
            startBtn.Name = "startBtn";
            startBtn.Size = new Size(61, 23);
            startBtn.TabIndex = 5;
            startBtn.Text = "Start";
            startBtn.UseVisualStyleBackColor = true;
            startBtn.Click += startBtn_Click;
            // 
            // stopBtn
            // 
            stopBtn.Location = new Point(206, 79);
            stopBtn.Margin = new Padding(3, 2, 3, 2);
            stopBtn.Name = "stopBtn";
            stopBtn.Size = new Size(64, 26);
            stopBtn.TabIndex = 6;
            stopBtn.Text = "Stop";
            stopBtn.UseVisualStyleBackColor = true;
            stopBtn.Click += stopBtn_Click;
            // 
            // importBtn
            // 
            importBtn.Location = new Point(144, 129);
            importBtn.Name = "importBtn";
            importBtn.Size = new Size(69, 26);
            importBtn.TabIndex = 7;
            importBtn.Text = "Import";
            importBtn.UseVisualStyleBackColor = true;
            importBtn.Click += importBtn_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(364, 225);
            Controls.Add(importBtn);
            Controls.Add(stopBtn);
            Controls.Add(startBtn);
            Margin = new Padding(3, 2, 3, 2);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion
        private Button startBtn;
        private Button stopBtn;
        private Button importBtn;
    }
}
