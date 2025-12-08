/*
 * MainForm.cs
 * Copyright (C) 2022 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using ASCOM.DriverAccess;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ASCOM.DeKoi.DeFocuserStandalone
{
    public partial class MainForm : Form
    {
        internal static string DRIVER_ID = "ASCOM.DeKoi.DeFocuserLite";
        internal const string SET_POSITION_COMMAND = "SetPosition";
        internal const string CALIBRATE_COMMAND = "Calibrate";
        internal const string IS_CALIBRATING_COMMAND = "IsCalibrating";
        internal const string IS_REVERSE_COMMAND = "IsReverse";
        internal const string SET_REVERSE_COMMAND = "SetReverse";
        internal static int HIGH_JUMP = 300;
        internal static int LOW_JUMP = 100;

        public const int WM_NCLBUTTONDOWN = 0x112;
        public const int HT_CAPTION = 0xf012;
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000;
                return cp;
            }
        }

        private Focuser device = null;

        private bool IsCalibrating => device.Action(IS_CALIBRATING_COMMAND, "") == "TRUE";

        private bool IsReverse => device.Action(IS_REVERSE_COMMAND, "") == "TRUE";

        public MainForm()
        {
            InitializeComponent();
            updateUI();
        }

        private void instantiateDevice()
        {
            if (device == null)
            {
                try
                {
                    device = new Focuser(DRIVER_ID);
                }
                catch (Exception)
                {
                    MessageBox.Show(this, "An error occurred while loading the focuser driver.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void updateUI()
        {
            bool connected = device != null && device.Connected;
            bool moving = connected && device.IsMoving;
            bool isCalibrating = connected && IsCalibrating;
            bool isReverse = connected && IsReverse;

            btnSettings.Enabled = !connected;
            backlashCompTextBox.Enabled = !connected;
            txtBoxTgtPos.Enabled = connected;
            btnMove.Enabled = connected && !moving && !isCalibrating;
            btnHalt.Enabled = connected && moving && !isCalibrating;
            btnSetPosition.Enabled = connected && !moving && !isCalibrating;
            btnMoveLeftHigh.Enabled = connected && !moving && !isCalibrating;
            btnMoveLeftLow.Enabled = connected && !moving && !isCalibrating;
            btnMoveRightLow.Enabled = connected && !moving && !isCalibrating;
            btnMoveRightHigh.Enabled = connected && !moving && !isCalibrating;
            btnSetZeroPos.Enabled = connected && !moving && !isCalibrating;
            btnCalibrate.Enabled = connected && !moving && !isCalibrating;

            if (connected)
            {
                this.lblCurPosVal.Text = device.Position.ToString();
                this.lblMaxStepsVal.Text = device.MaxStep.ToString();
            }
            else
            {
                this.lblCurPosVal.Text = "N/A";
                this.picIsMoving.Image = Properties.Resources.no;
            }
        }

        private void SetFocuserPosition(int targetPosition)
        {
            if (device != null && device.Connected)
            {
                if(targetPosition != device.Position)
                {
                    device.Action(SET_POSITION_COMMAND, targetPosition.ToString());

                    updateUI();
                }
            }
        }

        private void SetReverse(bool isReverse)
        {
            if (device != null && device.Connected)
            {
                device.Action(SET_REVERSE_COMMAND, isReverse.ToString());
            }
        }
        
        private void Calibrate()
        {
            if (device != null && device.Connected)
            {
                calibrateAsync().ConfigureAwait(false);
            }
        }

        private async Task move(int targetPosition)
        {
            if (device != null && device.Connected)
            {
                int delta = targetPosition - device.Position;
                if (delta > 0)
                {
                    int backlashCompSteps = Convert.ToInt32(backlashCompTextBox.Text);

                    // If we're moving OUT, we overshoot to deal with backlash...
                    device.Move(device.Position + backlashCompSteps + delta);

                    updateUI();

                    await waitForDeviceToStopMoving();

                    // Once the focuser has stopped moving, we tell it to move to
                    // its final position, thereby clearing the mechanical backlash.
                    device.Move(device.Position - backlashCompSteps);

                    await waitForDeviceToStopMoving();

                    updateUI();
                }
                else
                {
                    device.Move(targetPosition);

                    updateUI();

                    await waitForDeviceToStopMoving();

                    updateUI();
                }
            }
        }

        private async Task waitForDeviceToStopMoving()
        {
            await Task.Run(() =>
            {
                // Wait for the focuser to reach the desired position...
                while (device.IsMoving)
                {
                    Thread.Sleep(500);
                    Invoke(new Action(() =>
                    {
                        try
                        {
                            lblCurPosVal.Text = device.Position.ToString();
                            picIsMoving.Image = Properties.Resources.yes;
                            btnHalt.Enabled = true;
                            btnMove.Enabled = false;
                        }
                        catch (Exception) {; }
                    }));
                }

                Invoke(new Action(() =>
                {
                    picIsMoving.Image = Properties.Resources.no;
                    btnHalt.Enabled = false;
                    btnMove.Enabled = true;
                }));
            });
        }

        private async Task calibrateAsync()
        {
            device.Action(CALIBRATE_COMMAND, "");

            updateUI();

            await waitForDeviceToStopCalibrating();

            updateUI();
        }

        private async Task waitForDeviceToStopCalibrating()
        {
            await Task.Run(() =>
            {
                // Wait for the focuser to finish calibrating...
                while (IsCalibrating)
                {
                    Invoke(new Action(() =>
                    {
                        try
                        {
                            lblCurPosVal.Text = device.Position.ToString();
                            picIsMoving.Image = Properties.Resources.yes;
                            btnHalt.Enabled = true;
                            btnMove.Enabled = false;
                            btnSetPosition.Enabled = false;
                        }
                        catch (Exception) {; }
                    }));

                    Thread.Sleep(500);
                }

                Thread.Sleep(500);

                Invoke(new Action(() =>
                {
                    picIsMoving.Image = Properties.Resources.no;
                    btnHalt.Enabled = false;
                    btnMove.Enabled = true;
                    btnSetPosition.Enabled = true;
                    updateUI();
                }));
            });
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (device != null)
            {
                if (device.Connected)
                {
                    device.Connected = false;
                }
                device.Dispose();
                device = null;
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            instantiateDevice();
            if (device != null)
            {
                device.SetupDialog();
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (device != null && device.Connected)
            {
                device.Connected = false;
                btnConnect.Text = "Connect";
                btnConnect.Image = Properties.Resources.power_on;
                updateUI();
            }
            else
            {
                btnSettings.Enabled = false;
                btnConnect.Enabled = false;
                backlashCompTextBox.Enabled = false;

                // Hack to avoid having to use a thread/background worker.
                // This allows the previous lines to be immediately reflected in the UI.
                Application.DoEvents();

                instantiateDevice();
                if (device != null)
                {
                    try
                    {
                        // This can take a while. It can also throw...
                        device.Connected = true;
                        btnConnect.Text = "Disconnect";
                        btnConnect.Image = Properties.Resources.power_off;
                        updateUI();
                    }
                    catch (Exception)
                    {
                        MessageBox.Show(this, "An error occurred while connecting to the focuser.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        updateUI();
                    }
                }

                btnConnect.Enabled = true;
            }
        }

        private void btnMove_Click(object sender, EventArgs e)
        {
            int tgtPos;

            try
            {
                tgtPos = Convert.ToInt32(txtBoxTgtPos.Text);
                if (tgtPos < 0)
                {
                    throw new FormatException("The target position cannot be a negative number");
                }
                errorProvider.SetError(txtBoxTgtPos, string.Empty);
            }
            catch (Exception)
            {
                txtBoxTgtPos.Select(0, txtBoxTgtPos.Text.Length);
                errorProvider.SetError(txtBoxTgtPos, "Must be an integer (positive or negative)");
                return;
            }

            move(tgtPos).ConfigureAwait(false);
        }

        private void btnHalt_Click(object sender, EventArgs e)
        {
            if (device != null && device.Connected)
            {
                device.Halt();
            }
        }

        private void btnMoveLeftHigh_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Max(device.Position - HIGH_JUMP, 0);
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnMoveLeftLow_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Max(device.Position - LOW_JUMP, 0);
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnMoveRightLow_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Min(device.Position + LOW_JUMP, device.MaxStep);
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnMoveRightHigh_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Min(device.Position + HIGH_JUMP, device.MaxStep);
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnSetZeroPos_Click(object sender, EventArgs e)
        {
            device.Action("SetZeroPosition", "");
            updateUI();
        }

        private void backlashCompTextBox_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                int value = Convert.ToInt32(backlashCompTextBox.Text);
                if (value < 0)
                {
                    throw new FormatException("Backlash compensation cannot be a negative number");
                }
                errorProvider.SetError(backlashCompTextBox, string.Empty);
            }
            catch (Exception)
            {
                e.Cancel = true;
                backlashCompTextBox.Select(0, backlashCompTextBox.Text.Length);
                errorProvider.SetError(backlashCompTextBox, "Must be an integer (positive or negative)");
            }
        }

        private void txtBoxTgtPos_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnSetPosition_Click(object sender, EventArgs e)
        {
            int tgtPos;

            try
            {
                tgtPos = Convert.ToInt32(txtBoxTgtPos.Text);
                if (tgtPos < 0)
                {
                    throw new FormatException("The target position cannot be a negative number");
                }
                errorProvider.SetError(txtBoxTgtPos, string.Empty);
            }
            catch (Exception)
            {
                txtBoxTgtPos.Select(0, txtBoxTgtPos.Text.Length);
                errorProvider.SetError(txtBoxTgtPos, "Must be an integer (positive or negative)");
                return;
            }

            SetFocuserPosition(tgtPos);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Title_MouseDown(object sender, MouseEventArgs e)
        {
            OnMouseDown();
        }

        private void OnMouseDown()
        {
            ReleaseCapture();
            SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float curveSize = radius * 2F;

            path.StartFigure();
            path.AddArc(rect.X, rect.Y, curveSize, curveSize, 180, 90);
            path.AddArc(rect.Right - curveSize, rect.Y, curveSize, curveSize, 270, 90);
            path.AddArc(rect.Right - curveSize, rect.Bottom - curveSize, curveSize, curveSize, 0, 90);
            path.AddArc(rect.X, rect.Bottom - curveSize, curveSize, curveSize, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void FormRegionAndBorder(Form form, float radius, Graphics graph, Color borderColor, float borderSize)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                using (GraphicsPath roundPath = GetRoundedPath(form.ClientRectangle, radius))
                using (Pen penBorder = new Pen(borderColor, borderSize))
                using (Matrix transform = new Matrix())
                {
                    graph.SmoothingMode = SmoothingMode.AntiAlias;
                    form.Region = new Region(roundPath);
                    if (borderSize >= 1)
                    {
                        Rectangle rect = form.ClientRectangle;
                        float scaleX = 1.0F - ((borderSize + 1) / rect.Width);
                        float scaleY = 1.0F - ((borderSize + 1) / rect.Height);

                        transform.Scale(scaleX, scaleY);
                        transform.Translate(borderSize / 1.6F, borderSize / 1.6F);

                        graph.Transform = transform;
                        graph.DrawPath(penBorder, roundPath);
                    }
                }
            }
        }

        private void FormRegionAndBorder(Panel panel, float radius, Graphics graph, Color borderColor, float borderSize)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                using (GraphicsPath roundPath = GetRoundedPath(panel.ClientRectangle, radius))
                using (Pen penBorder = new Pen(borderColor, borderSize))
                using (Matrix transform = new Matrix())
                {
                    graph.SmoothingMode = SmoothingMode.AntiAlias;
                    panel.Region = new Region(roundPath);
                    if (borderSize >= 1)
                    {
                        Rectangle rect = panel.ClientRectangle;
                        float scaleX = 1.0F - ((borderSize + 1) / rect.Width);
                        float scaleY = 1.0F - ((borderSize + 1) / rect.Height);

                        transform.Scale(scaleX, scaleY);
                        transform.Translate(borderSize / 1.6F, borderSize / 1.6F);

                        graph.Transform = transform;
                        graph.DrawPath(penBorder, roundPath);
                    }
                }
            }
        }

        private void MainForm_Paint(object sender, PaintEventArgs e)
        {
            FormRegionAndBorder(this, 25, e.Graphics, Color.Blue, 0);
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            FormRegionAndBorder(panel1, 25, e.Graphics, Color.Blue, 0);
        }
        
        private void btnCalibrate_Click(object sender, EventArgs e)
        {
            Calibrate();
        }

        private void checkBoxReverse_Click(object sender, EventArgs e)
        {
            SetReverse(checkBoxReverse.Checked);
        }
    }
}
