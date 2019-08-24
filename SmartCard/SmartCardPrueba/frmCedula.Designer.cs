namespace SmartCardPrueba
{
	partial class frmCedula
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
			this.comboBox1 = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.panel2 = new System.Windows.Forms.Panel();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.lbApellido1 = new System.Windows.Forms.Label();
			this.lbApellido2 = new System.Windows.Forms.Label();
			this.lbNombres = new System.Windows.Forms.Label();
			this.lbNacionalidad = new System.Windows.Forms.Label();
			this.lbFechaNacimiento = new System.Windows.Forms.Label();
			this.lbLugarNacimiento = new System.Windows.Forms.Label();
			this.lbNroCedula = new System.Windows.Forms.Label();
			this.lbFechaExpedida = new System.Windows.Forms.Label();
			this.lbFechaVence = new System.Windows.Forms.Label();
			this.pictureFoto = new System.Windows.Forms.PictureBox();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.pictureFoto)).BeginInit();
			this.SuspendLayout();
			// 
			// comboBox1
			// 
			this.comboBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.comboBox1.FormattingEnabled = true;
			this.comboBox1.Location = new System.Drawing.Point(66, 12);
			this.comboBox1.Name = "comboBox1";
			this.comboBox1.Size = new System.Drawing.Size(713, 21);
			this.comboBox1.TabIndex = 2;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 15);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(40, 13);
			this.label1.TabIndex = 1;
			this.label1.Text = "Lector:";
			// 
			// label2
			// 
			this.label2.BackColor = System.Drawing.Color.White;
			this.label2.Font = new System.Drawing.Font("Tahoma", 20.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.label2.Location = new System.Drawing.Point(124, 12);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(568, 67);
			this.label2.TabIndex = 1;
			this.label2.Text = "Por favor Inserte la Cedula en el Lector con el Chip hacia Arriba";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// label3
			// 
			this.label3.BackColor = System.Drawing.Color.White;
			this.label3.Location = new System.Drawing.Point(79, 348);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(584, 23);
			this.label3.TabIndex = 0;
			this.label3.Text = "Listo.";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// panel1
			// 
			this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.pictureBox1);
			this.panel1.Location = new System.Drawing.Point(15, 39);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(767, 482);
			this.panel1.TabIndex = 0;
			// 
			// panel2
			// 
			this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.panel2.BackgroundImage = global::SmartCardPrueba.Properties.Resources.Cédula_de_Identidad_electrónica_de_Uruguay___Frente;
			this.panel2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
			this.panel2.Controls.Add(this.pictureFoto);
			this.panel2.Controls.Add(this.lbFechaVence);
			this.panel2.Controls.Add(this.lbFechaExpedida);
			this.panel2.Controls.Add(this.lbNroCedula);
			this.panel2.Controls.Add(this.lbLugarNacimiento);
			this.panel2.Controls.Add(this.lbFechaNacimiento);
			this.panel2.Controls.Add(this.lbNacionalidad);
			this.panel2.Controls.Add(this.lbNombres);
			this.panel2.Controls.Add(this.lbApellido2);
			this.panel2.Controls.Add(this.lbApellido1);
			this.panel2.Location = new System.Drawing.Point(15, 39);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(774, 484);
			this.panel2.TabIndex = 3;
			// 
			// pictureBox1
			// 
			this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox1.Image = global::SmartCardPrueba.Properties.Resources.identiv_sdi011;
			this.pictureBox1.Location = new System.Drawing.Point(0, 0);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(767, 482);
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			// 
			// lbApellido1
			// 
			this.lbApellido1.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbApellido1.Location = new System.Drawing.Point(310, 106);
			this.lbApellido1.Name = "lbApellido1";
			this.lbApellido1.Size = new System.Drawing.Size(235, 20);
			this.lbApellido1.TabIndex = 0;
			// 
			// lbApellido2
			// 
			this.lbApellido2.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbApellido2.Location = new System.Drawing.Point(310, 126);
			this.lbApellido2.Name = "lbApellido2";
			this.lbApellido2.Size = new System.Drawing.Size(235, 20);
			this.lbApellido2.TabIndex = 1;
			// 
			// lbNombres
			// 
			this.lbNombres.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbNombres.Location = new System.Drawing.Point(312, 166);
			this.lbNombres.Name = "lbNombres";
			this.lbNombres.Size = new System.Drawing.Size(233, 23);
			this.lbNombres.TabIndex = 2;
			// 
			// lbNacionalidad
			// 
			this.lbNacionalidad.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbNacionalidad.Location = new System.Drawing.Point(308, 211);
			this.lbNacionalidad.Name = "lbNacionalidad";
			this.lbNacionalidad.Size = new System.Drawing.Size(237, 23);
			this.lbNacionalidad.TabIndex = 3;
			// 
			// lbFechaNacimiento
			// 
			this.lbFechaNacimiento.AutoSize = true;
			this.lbFechaNacimiento.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbFechaNacimiento.Location = new System.Drawing.Point(310, 254);
			this.lbFechaNacimiento.Name = "lbFechaNacimiento";
			this.lbFechaNacimiento.Size = new System.Drawing.Size(0, 19);
			this.lbFechaNacimiento.TabIndex = 4;
			// 
			// lbLugarNacimiento
			// 
			this.lbLugarNacimiento.AutoSize = true;
			this.lbLugarNacimiento.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbLugarNacimiento.Location = new System.Drawing.Point(312, 297);
			this.lbLugarNacimiento.Name = "lbLugarNacimiento";
			this.lbLugarNacimiento.Size = new System.Drawing.Size(0, 19);
			this.lbLugarNacimiento.TabIndex = 5;
			// 
			// lbNroCedula
			// 
			this.lbNroCedula.Font = new System.Drawing.Font("Tahoma", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbNroCedula.Location = new System.Drawing.Point(311, 339);
			this.lbNroCedula.Name = "lbNroCedula";
			this.lbNroCedula.Size = new System.Drawing.Size(206, 32);
			this.lbNroCedula.TabIndex = 6;
			// 
			// lbFechaExpedida
			// 
			this.lbFechaExpedida.AutoSize = true;
			this.lbFechaExpedida.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbFechaExpedida.Location = new System.Drawing.Point(314, 399);
			this.lbFechaExpedida.Name = "lbFechaExpedida";
			this.lbFechaExpedida.Size = new System.Drawing.Size(0, 13);
			this.lbFechaExpedida.TabIndex = 7;
			// 
			// lbFechaVence
			// 
			this.lbFechaVence.AutoSize = true;
			this.lbFechaVence.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lbFechaVence.Location = new System.Drawing.Point(314, 443);
			this.lbFechaVence.Name = "lbFechaVence";
			this.lbFechaVence.Size = new System.Drawing.Size(0, 13);
			this.lbFechaVence.TabIndex = 8;
			// 
			// pictureFoto
			// 
			this.pictureFoto.Location = new System.Drawing.Point(41, 126);
			this.pictureFoto.Name = "pictureFoto";
			this.pictureFoto.Size = new System.Drawing.Size(227, 275);
			this.pictureFoto.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureFoto.TabIndex = 9;
			this.pictureFoto.TabStop = false;
			// 
			// frmCedula
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(791, 535);
			this.Controls.Add(this.panel1);
			this.Controls.Add(this.panel2);
			this.Controls.Add(this.comboBox1);
			this.Controls.Add(this.label1);
			this.Name = "frmCedula";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "frmCedula";
			this.Load += new System.EventHandler(this.frmCedula_Load);
			this.panel1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.pictureFoto)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.PictureBox pictureBox1;
		private System.Windows.Forms.ComboBox comboBox1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Panel panel2;
		private System.Windows.Forms.Label lbNombres;
		private System.Windows.Forms.Label lbApellido2;
		private System.Windows.Forms.Label lbApellido1;
		private System.Windows.Forms.Label lbFechaNacimiento;
		private System.Windows.Forms.Label lbNacionalidad;
		private System.Windows.Forms.Label lbLugarNacimiento;
		private System.Windows.Forms.Label lbFechaVence;
		private System.Windows.Forms.Label lbFechaExpedida;
		private System.Windows.Forms.Label lbNroCedula;
		private System.Windows.Forms.PictureBox pictureFoto;
	}
}