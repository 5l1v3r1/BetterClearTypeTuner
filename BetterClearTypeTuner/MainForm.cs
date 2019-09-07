﻿using BetterClearTypeTuner.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BetterClearTypeTuner
{
	public partial class MainForm : Form
	{
		bool dirty = false;

		public MainForm()
		{
			InitializeComponent();
			this.Text += " " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			UpdateStatus();
			cbFontAntialiasing.Focus();
		}

		private void ControlsChanged(object sender, EventArgs e)
		{
			if (rbGrayscale.Checked)
			{
				SetFontSmoothingTypeIfNotAlready(FontSmoothingType.Standard);
			}
			else if (rbRGB.Checked)
			{
				SetFontSmoothingTypeIfNotAlready(FontSmoothingType.ClearType);
				SetFontSmoothingIfNotAlready(FontSmoothingOrientation.RGB);
			}
			else if (rbBGR.Checked)
			{
				SetFontSmoothingTypeIfNotAlready(FontSmoothingType.ClearType);
				SetFontSmoothingIfNotAlready(FontSmoothingOrientation.BGR);
			}
			if (FontSmoothing.GetContrast() != (uint)nudContrast.Value)
			{
				FontSmoothing.SetContrast((uint)nudContrast.Value);
				dirty = true;
			}
			if (FontSmoothing.GetAntialiasingEnabled() != cbFontAntialiasing.Checked)
			{
				FontSmoothing.SetAntialiasingEnabled(cbFontAntialiasing.Checked);
				dirty = true;
			}
			if (dirty)
				UpdateStatus();
		}
		private void SetFontSmoothingIfNotAlready(FontSmoothingOrientation orientation)
		{
			if (FontSmoothing.GetFontSmoothingOrientation() != orientation)
			{
				FontSmoothing.SetFontSmoothingOrientation(orientation);
				dirty = true;
			}
		}
		private void SetFontSmoothingTypeIfNotAlready(FontSmoothingType type)
		{
			if (FontSmoothing.GetFontSmoothingType() != type)
			{
				FontSmoothing.SetFontSmoothingType(type);
				dirty = true;
			}
		}
		private void BtnRestoreDefaults_Click(object sender, EventArgs e)
		{
			DisableEvents();
			cbFontAntialiasing.Checked = true;
			rbRGB.Checked = true;
			nudContrast.Value = 1200;
			EnableEvents();
			ControlsChanged(sender, e);
		}

		#region Helpers
		private void UpdateStatus()
		{
			dirty = false;
			if (InvokeRequired)
				Invoke((Action)UpdateStatus);
			else
			{
				// Read font settings
				bool aaEnabled = FontSmoothing.GetAntialiasingEnabled();
				FontSmoothingOrientation orientation = FontSmoothing.GetFontSmoothingOrientation();
				FontSmoothingType smoothingType = FontSmoothing.GetFontSmoothingType();
				uint contrast = FontSmoothing.GetContrast();

				// Update UI controls
				DisableEvents();

				cbFontAntialiasing.Checked = aaEnabled;

				if (smoothingType == FontSmoothingType.Standard)
				{
					rbGrayscale.Checked = true;
					btnSet.Enabled = nudContrast.Enabled = false;
				}
				else
				{
					if (orientation == FontSmoothingOrientation.RGB)
					{
						rbRGB.Checked = true;
						btnSet.Enabled = nudContrast.Enabled = true;
					}
					else if (orientation == FontSmoothingOrientation.BGR)
					{
						rbBGR.Checked = true;
						btnSet.Enabled = nudContrast.Enabled = true;
					}
					else if (orientation == FontSmoothingOrientation.Unknown)
					{
						rbGrayscale.Checked = rbRGB.Checked = rbBGR.Checked = false;
						btnSet.Enabled = nudContrast.Enabled = false;
					}
				}

				nudContrast.Value = Clamp(contrast, (uint)nudContrast.Minimum, (uint)nudContrast.Maximum);

				rbGrayscale.Enabled = rbRGB.Enabled = rbBGR.Enabled = aaEnabled;

				if (!aaEnabled)
					nudContrast.Enabled = btnSet.Enabled = false;

				EnableEvents();

				string quick = "The quick brown fox jumps over the lazy dog. ";
				// Update status text
				if (aaEnabled)
				{
					if (smoothingType == FontSmoothingType.ClearType)
						status.Text = quick + orientation + " (Contrast " + contrast + ")";
					else
						status.Text = quick + "Grayscale (Contrast " + contrast + ")";
				}
				else
				{
					status.Text = quick + "Font Antialiasing is disabled.";
				}

				// Snapshot the sample text and render it zoomed-in
				CopyZoomedSnapshot();
			}
		}

		private uint Clamp(uint val, uint minimum, uint maximum)
		{
			if (val > maximum)
				val = maximum;
			if (val < minimum)
				val = minimum;
			return val;
		}

		private void CopyZoomedSnapshot()
		{
			this.Invalidate(true);
			//foreach (Control control in this.Controls)
			//	control.Invalidate();
			//.Invalidate();
			using (Bitmap src = new Bitmap(panelSmall.Width, panelSmall.Height))
			{
				panelSmall.DrawToBitmap(src, new Rectangle(0, 0, panelSmall.Width, panelSmall.Height));
				Image old = pbZoomed.Image;
				pbZoomed.Image = ScaleFast(src, 4);
				old?.Dispose();
			}
		}
		private Bitmap ScaleFast(Bitmap src, double scale)
		{
			int targetW = (int)(src.Width * scale);
			int targetH = (int)(src.Height * scale);
			int[] targetPixels = resizePixels(GetRawRGB(src), src.Width, src.Height, targetW, targetH);

			Bitmap target = new Bitmap(targetW, targetH, PixelFormat.Format32bppRgb);
			BitmapData targetData = target.LockBits(new Rectangle(0, 0, target.Width, target.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
			Marshal.Copy(targetPixels, 0, targetData.Scan0, targetPixels.Length);
			target.UnlockBits(targetData);
			return target;
		}
		private int[] GetRawRGB(Bitmap bmp)
		{
			BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
			int[] raw = new int[data.Width * data.Height];
			Marshal.Copy(data.Scan0, raw, 0, raw.Length);
			bmp.UnlockBits(data);
			return raw;
		}

		public int[] resizePixels(int[] pixels, int w1, int h1, int w2, int h2)
		{
			int[] temp = new int[w2 * h2];
			double x_ratio = w1 / (double)w2;
			double y_ratio = h1 / (double)h2;
			double px, py;
			for (int i = 0; i < h2; i++)
			{
				for (int j = 0; j < w2; j++)
				{
					px = Math.Floor(j * x_ratio);
					py = Math.Floor(i * y_ratio);
					temp[(i * w2) + j] = pixels[(int)((py * w1) + px)];
				}
			}
			return temp;
		}
		public void DisableEvents()
		{
			cbFontAntialiasing.CheckedChanged -= ControlsChanged;
			rbGrayscale.CheckedChanged -= ControlsChanged;
			rbRGB.CheckedChanged -= ControlsChanged;
			rbBGR.CheckedChanged -= ControlsChanged;
		}
		public void EnableEvents()
		{
			cbFontAntialiasing.CheckedChanged += ControlsChanged;
			rbGrayscale.CheckedChanged += ControlsChanged;
			rbRGB.CheckedChanged += ControlsChanged;
			rbBGR.CheckedChanged += ControlsChanged;
		}
		#endregion
	}
}
