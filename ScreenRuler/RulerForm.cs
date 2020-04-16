﻿using System;
using System.Drawing;
using System.Windows.Forms;
using ScreenRuler.Units;
using Bluegrams.Application;
using Bluegrams.Application.WinForms;
using ScreenRuler.Properties;
using System.ComponentModel;

namespace ScreenRuler
{
    [DesignerCategory("Form")]
    public partial class RulerForm : BaseForm
    {
        private WinFormsWindowManager manager;
        private WinFormsUpdateChecker updateChecker;
        private MouseTracker mouseTracker;
        private RulerPainter painter;

        public Settings Settings { get; set; }
        public MarkerCollection CustomMarkers { get; set; }

        public RulerForm()
        {
            Settings = new Settings();
            CustomMarkers = new MarkerCollection();
            manager = new WinFormsWindowManager(this) { AlwaysTrackResize = true };
            // Name all the properties we want to have persisted
            manager.ManageDefault();
            manager.Manage(nameof(Settings), nameof(TopMost), nameof(CustomMarkers));
            manager.Manage(nameof(ResizeMode), defaultValue: FormResizeMode.Horizontal);
            manager.Manage(nameof(Opacity), defaultValue: 1);
            manager.Initialize();
            InitializeComponent();
            this.MinimumSize = new Size(RulerPainter.RULER_WIDTH, RulerPainter.RULER_WIDTH);
            updateChecker = new WinFormsUpdateChecker(Program.UPDATE_URL, this, Program.UPDATE_MODE);
            mouseTracker = new MouseTracker(this);
            painter = new RulerPainter(this);
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.TopMost = true;
            this.MouseWheel += RulerForm_MouseWheel;
        }

        private UnitConverter getUnitConverter()
        {
            var screenSize = Screen.FromControl(this).Bounds.Size;
            return new UnitConverter(Settings.MeasuringUnit, screenSize, Settings.MonitorDpi);
        }

        private void RulerForm_Load(object sender, EventArgs e)
        {
            // Set some items of the context menu
            foreach (Enum item in Enum.GetValues(typeof(MeasuringUnit)))
            {
                comUnits.Items.Add(item.GetDescription());
            }
            // Reset the currently selected theme to avoid inconsistencies
            // caused by manual edits in the settings file.
            Settings.SelectedTheme = Settings.SelectedTheme;
            switch (this.Opacity*100)
            {
                case 100:
                    conHigh.Checked = true;
                    break;
                case 80:
                    conDefault.Checked = true;
                    break;
                case 60:
                    conLow.Checked = true;
                    break;
                case 40:
                    conVeryLow.Checked = true;
                    break;
            }
            // Set the marker limit
            CustomMarkers.Limit = Settings.MultiMarking ? int.MaxValue : 1;
            // Check for updates
            updateChecker.CheckForUpdates();
            // Start tracking mouse
            mouseTracker.Start();
        }

        private void RulerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            mouseTracker.Stop();
        }

        #region Input Events
        //Message result codes of WndProc that trigger resizing:
        // HTLEFT = 10 -> in left resize area 
        // HTRIGHT = 11 -> in right resize area
        // HTTOP = 12 -> in upper resize area
        // HTBOTTOM = 15 -> in lower resize area
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTBOTTOM = 15;

        private const int GRIP_OFFSET = 5;

        // Use Windows messages to handle resizing of the ruler at the edges
        // and moving of the cursor marker.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84) //WM_NCHITTEST (sent for all mouse events)
            {
                // Get mouse position and convert to app coordinates
                Point pos = Cursor.Position;
                pos = this.PointToClient(pos);
                // Check if inside grip area (5 pixels next to border)
                if (ResizeMode.HasFlag(FormResizeMode.Horizontal))
                {
                    if (pos.X <= GRIP_OFFSET)
                    {
                        m.Result = (IntPtr)HTLEFT;
                        return;
                    }
                    else if (pos.X >= this.ClientSize.Width - GRIP_OFFSET)
                    {
                        m.Result = (IntPtr)HTRIGHT;
                        return;
                    }
                }
                if (ResizeMode.HasFlag(FormResizeMode.Vertical))
                { 
                    if (pos.Y <= GRIP_OFFSET)
                    {
                        m.Result = (IntPtr)HTTOP;
                        return;
                    }
                    else if (pos.Y >= this.ClientSize.Height - GRIP_OFFSET)
                    {
                        m.Result = (IntPtr)HTBOTTOM;
                        return;
                    }
                }
            }
            // Pass return message down to base class
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:
                    toggleRulerMode();
                    break;
                case Keys.Escape:
                    conExit.PerformClick();
                    break;
                case Keys.Z:
                    conMeasure.PerformClick();
                    break;
                case Keys.S:
                    conTopmost.PerformClick();
                    break;
                case Keys.V:
                    toggleVertical();
                    break;
                case Keys.M:
                    conMarkCenter.PerformClick();
                    break;
                case Keys.T:
                    conMarkThirds.PerformClick();
                    break;
                case Keys.G:
                    conMarkGolden.PerformClick();
                    break;
                case Keys.P:
                    conMarkMouse.PerformClick();
                    break;
                case Keys.Delete:
                    conClearCustomMarker.PerformClick();
                    break;
                case Keys.C:
                    if (e.Control)
                    {
                        // copy size
                        Clipboard.SetText($"{Width}, {Height}");
                    }
                    else
                    {
                        // clear first custom marker
                        if (CustomMarkers.Markers.Count > 0)
                        {
                            CustomMarkers.Markers.RemoveFirst();
                            this.Invalidate();
                        }
                    }
                    break;
                case Keys.L:
                    CustomMarkers.AddMarker((Point)this.Size);
                    this.Invalidate();
                    break;
                case Keys.F1:
                    conHelp.PerformClick();
                    break;
                default:
                    if (e.Control) resizeKeyDown(e);
                    else if (e.Alt) dockKeyDown(e);
                    else moveKeyDown(e);
                    break;
            }
            base.OnKeyDown(e);
        }

        /// <summary>
        /// Handles moving key events.
        /// </summary>
        private void moveKeyDown(KeyEventArgs e)
        {
            int step = e.Shift ? Settings.MediumStep : 1;
            switch (e.KeyCode)
            {
                case Keys.Left:
                    this.Left -= step;
                    break;
                case Keys.Right:
                    this.Left += step;
                    break;
                case Keys.Up:
                    this.Top -= step;
                    break;
                case Keys.Down:
                    this.Top += step;
                    break;
            }
        }

        /// <summary>
        /// Handles resizing key events.
        /// </summary>
        private void resizeKeyDown(KeyEventArgs e)
        {
            int step = e.Shift ? Settings.MediumStep : 1;
            switch (e.KeyCode)
            {
                case Keys.Left:
                    this.Width -= step;
                    break;
                case Keys.Right:
                    this.Width += step;
                    break;
                case Keys.Up:
                    this.Height -= step;
                    break;
                case Keys.Down:
                    this.Height += step;
                    break;
            }
        }

        /// <summary>
        /// Handles key events for docking to borders.
        private void dockKeyDown(KeyEventArgs e)
        {
            Screen screen = Screen.FromControl(this);
            switch(e.KeyCode)
            {
                case Keys.Left:
                    this.Left = screen.WorkingArea.Left;
                    break;
                case Keys.Right:
                    this.Left = screen.WorkingArea.Right - this.Width;
                    break;
                case Keys.Up:
                    this.Top = screen.WorkingArea.Top;
                    break;
                case Keys.Down:
                    this.Top = screen.WorkingArea.Bottom - this.Height;
                    break;
            }
        }
        private void RulerForm_MouseWheel(object sender, MouseEventArgs e)
        {
            // Resize according to mouse scroll direction.
            var amount = Math.Sign(e.Delta);
            if (ModifierKeys.HasFlag(Keys.Shift))
                amount *= Settings.LargeStep;
            // Add to width or height according to current mode and mouse location
            if (ResizeMode == FormResizeMode.Horizontal)
            {
                Width += amount;
            }
            else if (ResizeMode == FormResizeMode.Vertical)
            {
                Height += amount;
            }
            else
            {
                if (e.Y > RulerPainter.RULER_WIDTH)
                    Height += amount;
                else Width += amount;
            }
        }

        private void RulerForm_MouseClick(object sender, MouseEventArgs e)
        {
            Marker marker = CustomMarkers.GetMarker(e.Location);
            if (marker != Marker.Default)
            {
                CustomLineForm lineForm = new CustomLineForm(marker,
                    getUnitConverter(), Settings.Theme);
                if (lineForm.ShowDialog(this) == DialogResult.OK)
                {
                    CustomMarkers.Markers.Remove(marker);
                    this.Invalidate();
                }
            }
        }

        private void RulerForm_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Add a marker at the cursor position.
            CustomMarkers.AddMarker(e.Location, ResizeMode == FormResizeMode.Vertical);
        }
        #endregion

        #region Draw Components
        protected override void OnPaint(PaintEventArgs e)
        {
            BufferedGraphics buffer;
            buffer = BufferedGraphicsManager.Current.Allocate(e.Graphics, e.ClipRectangle);
            // clear the graphics first
            buffer.Graphics.FillRectangle(new SolidBrush(TransparencyKey), e.ClipRectangle);
            // paint the ruler into buffer
            painter.Update(buffer.Graphics, Settings, ResizeMode);
            painter.PaintRuler();
            painter.PaintMarkers(CustomMarkers, mouseTracker.Position);
            // paint buffer onto screen
            buffer.Render();
            buffer.Dispose();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // draw transparent background
            e.Graphics.FillRectangle(
                new SolidBrush(TransparencyKey),
                new Rectangle(
                    RulerPainter.RULER_WIDTH, RulerPainter.RULER_WIDTH,
                    this.Width - RulerPainter.RULER_WIDTH, this.Height - RulerPainter.RULER_WIDTH
                )
            );
        }
        #endregion

        #region Ruler Mode
        private void toggleRulerMode()
        {
            ResizeMode = (FormResizeMode)(((int)ResizeMode + 1) % 3 + 1);
        }

        private void toggleVertical()
        {
            if (ResizeMode == FormResizeMode.Vertical)
            {
                int length = this.Height;
                ResizeMode = FormResizeMode.Horizontal;
                this.Width = length;
            }
            else
            {
                int length = this.Width;
                ResizeMode = FormResizeMode.Vertical;
                this.Height = length;
            }
        }
        #endregion

        #region Context Menu
        // Load current context menu state
        private void contxtMenu_Opening(object sender, CancelEventArgs e)
        {
            conMarkCenter.Checked = Settings.ShowCenterLine;
            conMarkThirds.Checked = Settings.ShowThirdLines;
            conMarkGolden.Checked = Settings.ShowGoldenLine;
            conTopmost.Checked = this.TopMost;
            conMarkMouse.Checked = Settings.ShowMouseLine;
            conOffsetLength.Checked = Settings.ShowOffsetLengthLabels;
            conMultiMarking.Checked = !Settings.MultiMarking;
            comUnits.SelectedIndex = (int)Settings.MeasuringUnit;
        }

        private void conRulerMode_DropDownOpening(object sender, EventArgs e)
        {
            conModeHorizontal.Checked = ResizeMode == FormResizeMode.Horizontal;
            conModeVertical.Checked = ResizeMode == FormResizeMode.Vertical;
            conModeTwoDimensional.Checked = ResizeMode == FormResizeMode.TwoDimensional;
        }

        private void conMeasure_Click(object sender, EventArgs e)
        {
            var overlay = new OverlayForm();
            overlay.TopMost = this.TopMost;
            if (overlay.ShowDialog() == DialogResult.OK)
            {
                this.ResizeMode = FormResizeMode.TwoDimensional;
                this.Location = overlay.WindowSelection.Location;
                this.Height = overlay.WindowSelection.Height;
                this.Width = overlay.WindowSelection.Width;
                this.CheckOutOfBounds();
                Settings.ShowOffsetLengthLabels = true;
            }
        }

        private void conMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void comUnits_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.MeasuringUnit = (MeasuringUnit)comUnits.SelectedIndex;
            this.Invalidate();
        }

        private void conMarkMouse_Click(object sender, EventArgs e)
        {
            Settings.ShowMouseLine = !Settings.ShowMouseLine;
            this.Invalidate();
        }

        private void conMarkCenter_Click(object sender, EventArgs e)
        {
            Settings.ShowCenterLine = !Settings.ShowCenterLine;
            this.Invalidate();
        }

        private void conMarkThirds_Click(object sender, EventArgs e)
        {
            Settings.ShowThirdLines = !Settings.ShowThirdLines;
            this.Invalidate();
        }

        private void conMarkGolden_Click(object sender, EventArgs e)
        {
            Settings.ShowGoldenLine = !Settings.ShowGoldenLine;
            this.Invalidate();
        }

        private void conOffsetLength_Click(object sender, EventArgs e)
        {
            Settings.ShowOffsetLengthLabels = !Settings.ShowOffsetLengthLabels;
            this.Invalidate();
        }

        private void conMultiMarking_Click(object sender, EventArgs e)
        {
            Settings.MultiMarking = !Settings.MultiMarking;
            CustomMarkers.Limit = Settings.MultiMarking ? int.MaxValue : 1;
            this.Invalidate();
        }

        private void conClearCustomMarker_Click(object sender, EventArgs e)
        {
            CustomMarkers.Markers.Clear();
            this.Invalidate();
        }

        private void conTopmost_Click(object sender, EventArgs e) => TopMost = !TopMost;

        private void changeRulerMode(object sender, EventArgs e)
        {
            foreach (ToolStripMenuItem it in conRulerMode.DropDownItems)
                it.Checked = false;
            ((ToolStripMenuItem)sender).Checked = true;
            ResizeMode = (FormResizeMode)Enum.Parse(typeof(FormResizeMode), (string)((ToolStripMenuItem)sender).Tag);
        }

        private void changeOpacity(object sender, EventArgs e)
        {
            foreach (ToolStripMenuItem it in conOpacity.DropDownItems)
                it.Checked = false;
            ((ToolStripMenuItem)sender).Checked = true;
            int opacity = int.Parse((String)((ToolStripMenuItem)sender).Tag);
            this.Opacity = (double)opacity / 100;
        }

        private void conLength_Click(object sender, EventArgs e)
        {
            SetSizeForm sizeForm = new SetSizeForm(this.Size, Settings);
            sizeForm.TopMost = this.TopMost;
            if (sizeForm.ShowDialog(this) == DialogResult.OK)
            {
                this.Width = (int)Math.Round(sizeForm.RulerWidth);
                this.Height = (int)Math.Round(sizeForm.RulerHeight);
            }
        }

        private void setToolTip()
        {
            // Tool tip
            if (Settings.ShowToolTip)
            {
                rulerToolTip.SetToolTip(this,
                    String.Format(Resources.ToolTipText, Width, Height, $"{Left}, {Top}"));
            }
            else rulerToolTip.SetToolTip(this, String.Empty);
        }

        private void RulerForm_Move(object sender, EventArgs e)
        {
            setToolTip();
        }

        private void RulerForm_SizeChanged(object sender, EventArgs e)
        {
            setToolTip();
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SettingsForm settingsForm = new SettingsForm(Settings);
            if (settingsForm.ShowDialog(this) == DialogResult.OK)
            {
                this.Invalidate();
            }
        }

        private void conCalibrate_Click(object sender, EventArgs e)
        {
            CalibrationForm scalingForm = new CalibrationForm(Settings);
            if (scalingForm.ShowDialog(this) == DialogResult.OK)
            {
                Settings.MonitorDpi = scalingForm.MonitorDpi;
                Settings.MonitorScaling = scalingForm.MonitorScaling;
            }
        }

        private void conHelp_Click(object sender, EventArgs e)
        {
            HelpForm helpForm = new HelpForm();
            helpForm.ShowDialog(this);
        }

        private void conAbout_Click(object sender, EventArgs e)
        {
            var resMan = new System.Resources.ResourceManager(this.GetType());
            var img = ((Icon)resMan.GetObject("$this.Icon")).ToBitmap();
            AboutForm aboutForm = new AboutForm(img);
            aboutForm.UpdateChecker = updateChecker;
            aboutForm.ShowDialog(this);
        }

        private void conExit_Click(object sender, EventArgs e) => Close();
        #endregion
    }
}
