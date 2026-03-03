/*
 * MainForm.cs
 * Copyright (C) 2022 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * DeFocuser Lite Mediator Application.
 * This app owns the serial connection to the focuser hardware and exposes it
 * to ASCOM driver clients via named pipes. It also provides a direct UI for
 * manual focuser control.
 */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ASCOM.DeKoi.DeFocuserMediator
{
    public partial class MainForm : Form
    {
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

        private SerialManager serialManager;
        private PipeServer pipeServer;

        private bool IsCalibrating => serialManager != null && serialManager.IsConnected && serialManager.GetIsCalibrating();

        private bool IsReverse => serialManager != null && serialManager.IsConnected && serialManager.GetIsReverse();

        public MainForm()
        {
            InitializeComponent();

            serialManager = new SerialManager();
            pipeServer = new PipeServer(serialManager);

            // Update UI when ASCOM client count changes
            pipeServer.ClientCountChanged += (count) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => lblClientsVal.Text = count.ToString()));
                }
                else
                {
                    lblClientsVal.Text = count.ToString();
                }
            };

            // Populate COM port list
            RefreshComPorts();

            updateUI();
        }

        private void RefreshComPorts()
        {
            comboBoxComPort.Items.Clear();
            comboBoxComPort.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            if (comboBoxComPort.Items.Count > 0)
            {
                comboBoxComPort.SelectedIndex = 0;
            }
        }

        private void updateUI()
        {
            bool connected = serialManager != null && serialManager.IsConnected;
            bool moving = false;
            bool isCalibrating = false;
            bool isReverse = false;

            if (connected)
            {
                try
                {
                    moving = serialManager.GetIsMoving();
                    isCalibrating = serialManager.GetIsCalibrating();
                    isReverse = serialManager.GetIsReverse();
                }
                catch (Exception) { }
            }

            comboBoxComPort.Enabled = !connected;
            chkAutoDetect.Enabled = !connected;
            btnRefreshPorts.Enabled = !connected;
            backlashCompTextBox.Enabled = !connected;

            txtBoxTgtPos.Enabled = connected;
            btnMove.Enabled = connected && !moving && !isCalibrating;
            // Allow halt during calibration so user can signal manual limits
            btnHalt.Enabled = connected && (moving || isCalibrating);
            btnSetPosition.Enabled = connected && !moving && !isCalibrating;
            btnMoveLeftHigh.Enabled = connected && !moving && !isCalibrating;
            btnMoveLeftLow.Enabled = connected && !moving && !isCalibrating;
            btnMoveRightLow.Enabled = connected && !moving && !isCalibrating;
            btnMoveRightHigh.Enabled = connected && !moving && !isCalibrating;
            btnSetZeroPos.Enabled = connected && !moving && !isCalibrating;
            btnCalibrate.Enabled = connected && !moving && !isCalibrating;

            if (connected)
            {
                try
                {
                    this.lblCurPosVal.Text = serialManager.GetPosition().ToString();
                    this.lblMaxStepsVal.Text = serialManager.GetMaxPosition().ToString();
                    this.checkBoxReverse.Checked = isReverse;
                }
                catch (Exception) { }
            }
            else
            {
                this.lblCurPosVal.Text = "N/A";
                this.lblMaxStepsVal.Text = "N/A";
                this.picIsMoving.Image = Properties.Resources.no;
            }
        }

        private void SetFocuserPosition(int targetPosition)
        {
            if (serialManager != null && serialManager.IsConnected)
            {
                try
                {
                    int currentPos = serialManager.GetPosition();
                    if (targetPosition != currentPos)
                    {
                        serialManager.SetPosition(targetPosition);
                        updateUI();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Error setting position: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SetReverse(bool isReverse)
        {
            if (serialManager != null && serialManager.IsConnected)
            {
                try
                {
                    serialManager.SetReverse(isReverse);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Error setting reverse: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Calibrate()
        {
            if (serialManager != null && serialManager.IsConnected)
            {
                calibrateAsync().ConfigureAwait(false);
            }
        }

        private async Task move(int targetPosition)
        {
            if (serialManager != null && serialManager.IsConnected)
            {
                try
                {
                    int currentPos = serialManager.GetPosition();
                    int delta = targetPosition - currentPos;

                    if (delta > 0)
                    {
                        int backlashCompSteps = Convert.ToInt32(backlashCompTextBox.Text);

                        // If we're moving OUT, we overshoot to deal with backlash...
                        serialManager.Move(currentPos + backlashCompSteps + delta);

                        updateUI();

                        await waitForDeviceToStopMoving();

                        // Once the focuser has stopped moving, we tell it to move to
                        // its final position, thereby clearing the mechanical backlash.
                        int newPos = serialManager.GetPosition();
                        serialManager.Move(newPos - backlashCompSteps);

                        await waitForDeviceToStopMoving();

                        updateUI();
                    }
                    else
                    {
                        serialManager.Move(targetPosition);

                        updateUI();

                        await waitForDeviceToStopMoving();

                        updateUI();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Error moving focuser: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task waitForDeviceToStopMoving()
        {
            await Task.Run(() =>
            {
                // Wait for the focuser to reach the desired position...
                while (serialManager.GetIsMoving())
                {
                    Thread.Sleep(500);
                    Invoke(new Action(() =>
                    {
                        try
                        {
                            lblCurPosVal.Text = serialManager.GetPosition().ToString();
                            picIsMoving.Image = Properties.Resources.yes;
                            btnHalt.Enabled = true;
                            btnMove.Enabled = false;
                        }
                        catch (Exception) { }
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
            try
            {
                serialManager.Calibrate();

                updateUI();

                await waitForDeviceToStopCalibrating();

                updateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Calibration error: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task waitForDeviceToStopCalibrating()
        {
            await Task.Run(() =>
            {
                // Wait for the focuser to finish calibrating...
                while (serialManager.GetIsCalibrating())
                {
                    Invoke(new Action(() =>
                    {
                        try
                        {
                            lblCurPosVal.Text = serialManager.GetPosition().ToString();
                            picIsMoving.Image = Properties.Resources.yes;
                            // Keep halt enabled so user can signal manual limits
                            btnHalt.Enabled = true;
                            btnMove.Enabled = false;
                            btnSetPosition.Enabled = false;
                        }
                        catch (Exception) { }
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
            // If ASCOM clients are still connected, minimize to tray instead of closing
            if (pipeServer != null && pipeServer.ConnectedClientCount > 0)
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(3000, "DeFocuser Lite",
                    "Still serving " + pipeServer.ConnectedClientCount + " ASCOM client(s). Right-click tray icon to force close.",
                    ToolTipIcon.Info);
                return;
            }

            // Clean shutdown
            if (pipeServer != null)
            {
                pipeServer.Dispose();
                pipeServer = null;
            }

            if (serialManager != null)
            {
                if (serialManager.IsConnected)
                {
                    serialManager.Disconnect();
                }
                serialManager.Dispose();
                serialManager = null;
            }

            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (serialManager != null && serialManager.IsConnected)
            {
                // Disconnect
                if (pipeServer.ConnectedClientCount > 0)
                {
                    var result = MessageBox.Show(this,
                        "There are " + pipeServer.ConnectedClientCount + " ASCOM client(s) still connected. Disconnecting will break their connection. Continue?",
                        "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (result != DialogResult.Yes)
                        return;
                }

                pipeServer.Stop();
                serialManager.Disconnect();
                btnConnect.Text = "Connect";
                btnConnect.Image = Properties.Resources.power_on;
                updateUI();
            }
            else
            {
                // Connect
                comboBoxComPort.Enabled = false;
                chkAutoDetect.Enabled = false;
                btnRefreshPorts.Enabled = false;
                btnConnect.Enabled = false;
                backlashCompTextBox.Enabled = false;

                // Allow the UI to update before the potentially slow connection
                Application.DoEvents();

                try
                {
                    string selectedPort = chkAutoDetect.Checked ? null : (string)comboBoxComPort.SelectedItem;
                    serialManager.Connect(selectedPort, chkAutoDetect.Checked);

                    // Start the pipe server so ASCOM clients can connect
                    pipeServer.Start();

                    btnConnect.Text = "Disconnect";
                    btnConnect.Image = Properties.Resources.power_off;
                    updateUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to connect: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    updateUI();
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
            if (serialManager != null && serialManager.IsConnected)
            {
                try
                {
                    serialManager.Halt();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Halt error: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnMoveLeftHigh_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Max(serialManager.GetPosition() - HIGH_JUMP, 0);
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnMoveLeftLow_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Max(serialManager.GetPosition() - LOW_JUMP, 0);
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnMoveRightLow_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Min(serialManager.GetPosition() + LOW_JUMP, serialManager.GetMaxPosition());
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnMoveRightHigh_Click(object sender, EventArgs e)
        {
            int tgtPos = Math.Min(serialManager.GetPosition() + HIGH_JUMP, serialManager.GetMaxPosition());
            move(tgtPos).ConfigureAwait(false);
        }

        private void btnSetZeroPos_Click(object sender, EventArgs e)
        {
            try
            {
                serialManager.SetZeroPosition();
                updateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private void btnRefreshPorts_Click(object sender, EventArgs e)
        {
            RefreshComPorts();
        }

        // Tray icon handlers
        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void trayMenuShow_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void trayMenuForceClose_Click(object sender, EventArgs e)
        {
            // Force close - disconnect everything
            if (pipeServer != null)
            {
                pipeServer.Stop();
            }

            if (serialManager != null && serialManager.IsConnected)
            {
                serialManager.Disconnect();
            }

            notifyIcon.Visible = false;
            Application.Exit();
        }

        private void chkAutoDetect_CheckedChanged(object sender, EventArgs e)
        {
            comboBoxComPort.Enabled = !chkAutoDetect.Checked;
        }
    }
}
