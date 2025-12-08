/*
 * FocuserSetupDialogForm.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using ASCOM.Utilities;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ASCOM.DeKoi
{
    // Form not registered for COM!
    [ComVisible(false)]

    public partial class FocuserSetupDialogForm : Form
    {
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

        // Holder for a reference to the driver instance
        private readonly Focuser focuser;

        public FocuserSetupDialogForm(Focuser focuser)
        {
            InitializeComponent();

            // Save the provided driver instance for use within the setup dialog
            this.focuser = focuser;
        }

        private void FocuserSetupDialogForm_Load(object sender, EventArgs e)
        {
            chkAutoDetect.Checked = Focuser.autoDetectComPort;

            comboBoxComPort.Enabled = !chkAutoDetect.Checked;

            // Set the list of COM ports to those that are currently available
            comboBoxComPort.Items.Clear();
            // Use System.IO because it's static
            comboBoxComPort.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            // Select the current port if possible
            if (Focuser.comPortOverride != null && comboBoxComPort.Items.Contains(Focuser.comPortOverride))
            {
                comboBoxComPort.SelectedItem = Focuser.comPortOverride;
            }

            chkTrace.Checked = focuser.tl.Enabled;
        }

        private void CmdOK_Click(object sender, EventArgs e)
        {
            if (!Validate())
            {
                DialogResult = DialogResult.None;
            }

            Focuser.autoDetectComPort = chkAutoDetect.Checked;
            Focuser.comPortOverride = (string)comboBoxComPort.SelectedItem;
            focuser.tl.Enabled = chkTrace.Checked;
        }

        private void CmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ChkAutoDetect_CheckedChanged(object sender, EventArgs e)
        {
            comboBoxComPort.Enabled = !((CheckBox)sender).Checked;
        }

        private void BrowseToHomepage(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://github.com/jlecomte/ascom-oag-focuser");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void comPortOverrideLabel_Click(object sender, EventArgs e)
        {

        }

        private void label1_MouseDown(object sender, MouseEventArgs e)
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

        private void FocuserSetupDialogForm_Paint(object sender, PaintEventArgs e)
        {
            FormRegionAndBorder(this, 25, e.Graphics, Color.Blue, 0);
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            FormRegionAndBorder(panel1, 25, e.Graphics, Color.Blue, 0);
        }
    }
}