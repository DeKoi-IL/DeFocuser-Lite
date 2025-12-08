/*
 * MainForm.Designer.cs
 * Copyright (C) 2022 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

namespace ASCOM.DeKoi.DeFocuserStandalone
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.lblCurPos = new System.Windows.Forms.Label();
            this.lblTgtPos = new System.Windows.Forms.Label();
            this.txtBoxTgtPos = new System.Windows.Forms.TextBox();
            this.btnMove = new System.Windows.Forms.Button();
            this.lblIsMoving = new System.Windows.Forms.Label();
            this.btnMoveLeftHigh = new System.Windows.Forms.Button();
            this.btnMoveLeftLow = new System.Windows.Forms.Button();
            this.btnMoveRightLow = new System.Windows.Forms.Button();
            this.btnMoveRightHigh = new System.Windows.Forms.Button();
            this.btnSetZeroPos = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.picIsMoving = new System.Windows.Forms.PictureBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.lblCurPosVal = new System.Windows.Forms.Label();
            this.errorProvider = new System.Windows.Forms.ErrorProvider(this.components);
            this.backlashCompLabel = new System.Windows.Forms.Label();
            this.backlashCompTextBox = new System.Windows.Forms.TextBox();
            this.btnHalt = new System.Windows.Forms.Button();
            this.btnSetPosition = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnCalibrate = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.Title = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.lblMaxStepsVal = new System.Windows.Forms.Label();
            this.checkBoxReverse = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.picIsMoving)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblCurPos
            // 
            this.lblCurPos.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblCurPos.AutoSize = true;
            this.lblCurPos.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurPos.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.lblCurPos.Location = new System.Drawing.Point(34, 166);
            this.lblCurPos.Name = "lblCurPos";
            this.lblCurPos.Size = new System.Drawing.Size(100, 17);
            this.lblCurPos.TabIndex = 1;
            this.lblCurPos.Text = "Current Position:";
            // 
            // lblTgtPos
            // 
            this.lblTgtPos.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblTgtPos.AutoSize = true;
            this.lblTgtPos.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTgtPos.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.lblTgtPos.Location = new System.Drawing.Point(30, 262);
            this.lblTgtPos.Name = "lblTgtPos";
            this.lblTgtPos.Size = new System.Drawing.Size(94, 17);
            this.lblTgtPos.TabIndex = 1;
            this.lblTgtPos.Text = "Target Position:";
            // 
            // txtBoxTgtPos
            // 
            this.txtBoxTgtPos.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.txtBoxTgtPos.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtBoxTgtPos.Location = new System.Drawing.Point(126, 258);
            this.txtBoxTgtPos.Name = "txtBoxTgtPos";
            this.txtBoxTgtPos.Size = new System.Drawing.Size(52, 23);
            this.txtBoxTgtPos.TabIndex = 3;
            this.txtBoxTgtPos.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtBoxTgtPos.TextChanged += new System.EventHandler(this.txtBoxTgtPos_TextChanged);
            // 
            // btnMove
            // 
            this.btnMove.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnMove.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnMove.FlatAppearance.BorderSize = 0;
            this.btnMove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMove.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnMove.Location = new System.Drawing.Point(250, 256);
            this.btnMove.Name = "btnMove";
            this.btnMove.Size = new System.Drawing.Size(56, 26);
            this.btnMove.TabIndex = 4;
            this.btnMove.Text = "Move";
            this.btnMove.UseVisualStyleBackColor = false;
            this.btnMove.Click += new System.EventHandler(this.btnMove_Click);
            // 
            // lblIsMoving
            // 
            this.lblIsMoving.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblIsMoving.AutoSize = true;
            this.lblIsMoving.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblIsMoving.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.lblIsMoving.Location = new System.Drawing.Point(208, 309);
            this.lblIsMoving.Name = "lblIsMoving";
            this.lblIsMoving.Size = new System.Drawing.Size(65, 17);
            this.lblIsMoving.TabIndex = 1;
            this.lblIsMoving.Text = "Is Moving:";
            // 
            // btnMoveLeftHigh
            // 
            this.btnMoveLeftHigh.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnMoveLeftHigh.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnMoveLeftHigh.FlatAppearance.BorderSize = 0;
            this.btnMoveLeftHigh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMoveLeftHigh.Font = new System.Drawing.Font("Roboto Condensed", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnMoveLeftHigh.Location = new System.Drawing.Point(37, 303);
            this.btnMoveLeftHigh.Name = "btnMoveLeftHigh";
            this.btnMoveLeftHigh.Size = new System.Drawing.Size(39, 26);
            this.btnMoveLeftHigh.TabIndex = 6;
            this.btnMoveLeftHigh.Text = "<<";
            this.btnMoveLeftHigh.UseVisualStyleBackColor = false;
            this.btnMoveLeftHigh.Click += new System.EventHandler(this.btnMoveLeftHigh_Click);
            // 
            // btnMoveLeftLow
            // 
            this.btnMoveLeftLow.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnMoveLeftLow.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnMoveLeftLow.FlatAppearance.BorderSize = 0;
            this.btnMoveLeftLow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMoveLeftLow.Font = new System.Drawing.Font("Roboto Condensed", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnMoveLeftLow.Location = new System.Drawing.Point(81, 303);
            this.btnMoveLeftLow.Name = "btnMoveLeftLow";
            this.btnMoveLeftLow.Size = new System.Drawing.Size(29, 26);
            this.btnMoveLeftLow.TabIndex = 7;
            this.btnMoveLeftLow.Text = "<";
            this.btnMoveLeftLow.UseVisualStyleBackColor = false;
            this.btnMoveLeftLow.Click += new System.EventHandler(this.btnMoveLeftLow_Click);
            // 
            // btnMoveRightLow
            // 
            this.btnMoveRightLow.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnMoveRightLow.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnMoveRightLow.FlatAppearance.BorderSize = 0;
            this.btnMoveRightLow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMoveRightLow.Font = new System.Drawing.Font("Roboto Condensed", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnMoveRightLow.Location = new System.Drawing.Point(116, 303);
            this.btnMoveRightLow.Name = "btnMoveRightLow";
            this.btnMoveRightLow.Size = new System.Drawing.Size(29, 26);
            this.btnMoveRightLow.TabIndex = 8;
            this.btnMoveRightLow.Text = ">";
            this.btnMoveRightLow.UseVisualStyleBackColor = false;
            this.btnMoveRightLow.Click += new System.EventHandler(this.btnMoveRightLow_Click);
            // 
            // btnMoveRightHigh
            // 
            this.btnMoveRightHigh.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnMoveRightHigh.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnMoveRightHigh.FlatAppearance.BorderSize = 0;
            this.btnMoveRightHigh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMoveRightHigh.Font = new System.Drawing.Font("Roboto Condensed", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnMoveRightHigh.Location = new System.Drawing.Point(151, 303);
            this.btnMoveRightHigh.Name = "btnMoveRightHigh";
            this.btnMoveRightHigh.Size = new System.Drawing.Size(37, 26);
            this.btnMoveRightHigh.TabIndex = 9;
            this.btnMoveRightHigh.Text = ">>";
            this.btnMoveRightHigh.UseVisualStyleBackColor = false;
            this.btnMoveRightHigh.Click += new System.EventHandler(this.btnMoveRightHigh_Click);
            // 
            // btnSetZeroPos
            // 
            this.btnSetZeroPos.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnSetZeroPos.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnSetZeroPos.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnSetZeroPos.FlatAppearance.BorderSize = 0;
            this.btnSetZeroPos.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSetZeroPos.Font = new System.Drawing.Font("Roboto Condensed", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSetZeroPos.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.btnSetZeroPos.Location = new System.Drawing.Point(55, 349);
            this.btnSetZeroPos.Name = "btnSetZeroPos";
            this.btnSetZeroPos.Size = new System.Drawing.Size(142, 31);
            this.btnSetZeroPos.TabIndex = 10;
            this.btnSetZeroPos.Text = "Set Zero position!";
            this.btnSetZeroPos.UseVisualStyleBackColor = false;
            this.btnSetZeroPos.Click += new System.EventHandler(this.btnSetZeroPos_Click);
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnSettings.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnSettings.FlatAppearance.BorderSize = 0;
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettings.Font = new System.Drawing.Font("Roboto Condensed", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettings.Image = global::ASCOM.DeKoi.DeFocuserStandalone.Properties.Resources.gears;
            this.btnSettings.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnSettings.Location = new System.Drawing.Point(62, 24);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.btnSettings.Size = new System.Drawing.Size(126, 80);
            this.btnSettings.TabIndex = 0;
            this.btnSettings.Text = "Settings";
            this.btnSettings.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnSettings.UseVisualStyleBackColor = false;
            this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // picIsMoving
            // 
            this.picIsMoving.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.picIsMoving.BackColor = System.Drawing.Color.Transparent;
            this.picIsMoving.Image = global::ASCOM.DeKoi.DeFocuserStandalone.Properties.Resources.no;
            this.picIsMoving.Location = new System.Drawing.Point(285, 302);
            this.picIsMoving.Name = "picIsMoving";
            this.picIsMoving.Size = new System.Drawing.Size(32, 32);
            this.picIsMoving.TabIndex = 8;
            this.picIsMoving.TabStop = false;
            // 
            // btnConnect
            // 
            this.btnConnect.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnConnect.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnConnect.FlatAppearance.BorderSize = 0;
            this.btnConnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnConnect.Font = new System.Drawing.Font("Roboto Condensed", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnConnect.Image = global::ASCOM.DeKoi.DeFocuserStandalone.Properties.Resources.power_on;
            this.btnConnect.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnConnect.Location = new System.Drawing.Point(212, 24);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Padding = new System.Windows.Forms.Padding(0, 5, 0, 5);
            this.btnConnect.Size = new System.Drawing.Size(126, 80);
            this.btnConnect.TabIndex = 1;
            this.btnConnect.Text = "Connect";
            this.btnConnect.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnConnect.UseVisualStyleBackColor = false;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // lblCurPosVal
            // 
            this.lblCurPosVal.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblCurPosVal.AutoSize = true;
            this.lblCurPosVal.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurPosVal.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.lblCurPosVal.Location = new System.Drawing.Point(142, 166);
            this.lblCurPosVal.Name = "lblCurPosVal";
            this.lblCurPosVal.Size = new System.Drawing.Size(30, 17);
            this.lblCurPosVal.TabIndex = 1;
            this.lblCurPosVal.Text = "N/A";
            // 
            // errorProvider
            // 
            this.errorProvider.ContainerControl = this;
            // 
            // backlashCompLabel
            // 
            this.backlashCompLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.backlashCompLabel.AutoSize = true;
            this.backlashCompLabel.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.backlashCompLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.backlashCompLabel.Location = new System.Drawing.Point(34, 130);
            this.backlashCompLabel.Name = "backlashCompLabel";
            this.backlashCompLabel.Size = new System.Drawing.Size(143, 17);
            this.backlashCompLabel.TabIndex = 1;
            this.backlashCompLabel.Text = "Backlash compensation:";
            // 
            // backlashCompTextBox
            // 
            this.backlashCompTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.backlashCompTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.backlashCompTextBox.Location = new System.Drawing.Point(183, 127);
            this.backlashCompTextBox.Name = "backlashCompTextBox";
            this.backlashCompTextBox.Size = new System.Drawing.Size(60, 23);
            this.backlashCompTextBox.TabIndex = 2;
            this.backlashCompTextBox.Text = "0";
            this.backlashCompTextBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.backlashCompTextBox.Validating += new System.ComponentModel.CancelEventHandler(this.backlashCompTextBox_Validating);
            // 
            // btnHalt
            // 
            this.btnHalt.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnHalt.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnHalt.FlatAppearance.BorderSize = 0;
            this.btnHalt.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnHalt.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnHalt.Location = new System.Drawing.Point(312, 256);
            this.btnHalt.Name = "btnHalt";
            this.btnHalt.Size = new System.Drawing.Size(55, 26);
            this.btnHalt.TabIndex = 5;
            this.btnHalt.Text = "Halt!";
            this.btnHalt.UseVisualStyleBackColor = false;
            this.btnHalt.Click += new System.EventHandler(this.btnHalt_Click);
            // 
            // btnSetPosition
            // 
            this.btnSetPosition.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnSetPosition.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnSetPosition.FlatAppearance.BorderSize = 0;
            this.btnSetPosition.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSetPosition.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSetPosition.Location = new System.Drawing.Point(188, 256);
            this.btnSetPosition.Name = "btnSetPosition";
            this.btnSetPosition.Size = new System.Drawing.Size(56, 26);
            this.btnSetPosition.TabIndex = 11;
            this.btnSetPosition.Text = "Set";
            this.btnSetPosition.UseVisualStyleBackColor = false;
            this.btnSetPosition.Click += new System.EventHandler(this.btnSetPosition_Click);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(30)))), ((int)(((byte)(54)))));
            this.panel1.Controls.Add(this.checkBoxReverse);
            this.panel1.Controls.Add(this.lblMaxStepsVal);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.btnCalibrate);
            this.panel1.Controls.Add(this.btnConnect);
            this.panel1.Controls.Add(this.btnSetPosition);
            this.panel1.Controls.Add(this.lblCurPos);
            this.panel1.Controls.Add(this.btnHalt);
            this.panel1.Controls.Add(this.lblTgtPos);
            this.panel1.Controls.Add(this.backlashCompTextBox);
            this.panel1.Controls.Add(this.txtBoxTgtPos);
            this.panel1.Controls.Add(this.backlashCompLabel);
            this.panel1.Controls.Add(this.btnMove);
            this.panel1.Controls.Add(this.lblCurPosVal);
            this.panel1.Controls.Add(this.lblIsMoving);
            this.panel1.Controls.Add(this.picIsMoving);
            this.panel1.Controls.Add(this.btnSettings);
            this.panel1.Controls.Add(this.btnMoveLeftHigh);
            this.panel1.Controls.Add(this.btnSetZeroPos);
            this.panel1.Controls.Add(this.btnMoveLeftLow);
            this.panel1.Controls.Add(this.btnMoveRightHigh);
            this.panel1.Controls.Add(this.btnMoveRightLow);
            this.panel1.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.panel1.Location = new System.Drawing.Point(11, 72);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(387, 418);
            this.panel1.TabIndex = 12;
            this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint);
            // 
            // btnCalibrate
            // 
            this.btnCalibrate.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnCalibrate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.btnCalibrate.FlatAppearance.BorderSize = 0;
            this.btnCalibrate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCalibrate.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCalibrate.Location = new System.Drawing.Point(217, 349);
            this.btnCalibrate.Name = "btnCalibrate";
            this.btnCalibrate.Size = new System.Drawing.Size(145, 31);
            this.btnCalibrate.TabIndex = 12;
            this.btnCalibrate.Text = "Calibrate";
            this.btnCalibrate.UseVisualStyleBackColor = false;
            this.btnCalibrate.Click += new System.EventHandler(this.btnCalibrate_Click);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.BackgroundImage = global::ASCOM.DeKoi.DeFocuserStandalone.Properties.Resources.remove;
            this.btnClose.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.btnClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.btnClose.Location = new System.Drawing.Point(352, 0);
            this.btnClose.Margin = new System.Windows.Forms.Padding(2);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(57, 57);
            this.btnClose.TabIndex = 12;
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // Title
            // 
            this.Title.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.Title.Dock = System.Windows.Forms.DockStyle.Top;
            this.Title.Font = new System.Drawing.Font("Roboto Condensed Medium", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.Title.Location = new System.Drawing.Point(0, 0);
            this.Title.Name = "Title";
            this.Title.Padding = new System.Windows.Forms.Padding(0, 15, 0, 0);
            this.Title.Size = new System.Drawing.Size(409, 70);
            this.Title.TabIndex = 13;
            this.Title.Text = "DeFocuser Controller";
            this.Title.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.Title.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Title_MouseDown);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.label1.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.label1.Location = new System.Drawing.Point(34, 196);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 25);
            this.label1.TabIndex = 13;
            this.label1.Text = "Max Steps:";
            // 
            // lblMaxStepsVal
            // 
            this.lblMaxStepsVal.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblMaxStepsVal.Font = new System.Drawing.Font("Roboto Condensed", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMaxStepsVal.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.lblMaxStepsVal.Location = new System.Drawing.Point(142, 196);
            this.lblMaxStepsVal.Name = "lblMaxStepsVal";
            this.lblMaxStepsVal.Size = new System.Drawing.Size(83, 17);
            this.lblMaxStepsVal.TabIndex = 14;
            this.lblMaxStepsVal.Text = "N/A";
            // 
            // checkBoxReverse
            // 
            this.checkBoxReverse.Font = new System.Drawing.Font("Roboto Condensed", 10.2F);
            this.checkBoxReverse.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(158)))), ((int)(((byte)(161)))), ((int)(((byte)(176)))));
            this.checkBoxReverse.Location = new System.Drawing.Point(37, 225);
            this.checkBoxReverse.Name = "checkBoxReverse";
            this.checkBoxReverse.Size = new System.Drawing.Size(83, 19);
            this.checkBoxReverse.TabIndex = 15;
            this.checkBoxReverse.Text = "Reverse";
            this.checkBoxReverse.UseVisualStyleBackColor = true;
            this.checkBoxReverse.Click += new System.EventHandler(this.checkBoxReverse_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(51)))), ((int)(((byte)(73)))));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(409, 501);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.Title);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "DeFocuser Lite";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.MainForm_Paint);
            ((System.ComponentModel.ISupportInitialize)(this.picIsMoving)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label lblCurPos;
        private System.Windows.Forms.Label lblTgtPos;
        private System.Windows.Forms.TextBox txtBoxTgtPos;
        private System.Windows.Forms.Button btnMove;
        private System.Windows.Forms.Label lblIsMoving;
        private System.Windows.Forms.PictureBox picIsMoving;
        private System.Windows.Forms.Button btnMoveLeftHigh;
        private System.Windows.Forms.Button btnMoveLeftLow;
        private System.Windows.Forms.Button btnMoveRightLow;
        private System.Windows.Forms.Button btnMoveRightHigh;
        private System.Windows.Forms.Button btnSetZeroPos;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Label lblCurPosVal;
        private System.Windows.Forms.ErrorProvider errorProvider;
        private System.Windows.Forms.Label backlashCompLabel;
        private System.Windows.Forms.TextBox backlashCompTextBox;
        private System.Windows.Forms.Button btnHalt;
        private System.Windows.Forms.Button btnSetPosition;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label Title;
        private System.Windows.Forms.Button btnCalibrate;
        private System.Windows.Forms.Label lblMaxStepsVal;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBoxReverse;
    }
}

