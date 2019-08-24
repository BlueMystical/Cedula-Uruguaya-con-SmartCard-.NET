using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using SmartcardLibrary;

namespace SmartCardPrueba
{
	public partial class frmCedula : Form
	{
		private SmartcardManager manager = SmartcardManager.GetManager(); //<-Representa al Lector de Tarjetas		
		private string readerName = string.Empty; //<- Nombre del Lector de Tarjetas Seleccionado
		 //[OPCIONAL] Delegado para Detectar la Insercion/Retiro de Tarjetas del lector
		private delegate void OnReaderChangeStatus_CallBack(SmartcardState pState);
		private OnReaderChangeStatus_CallBack Delegado1;

		private delegate void DelegateType(string pMensaje); //<- Declarar un Delegado 
		private DelegateType Delegado2;
		Thread MiProceso1;          //En este proceso se Cargan los datos.

		private TLV_Cedula Datos_Cedula = null;

		public frmCedula()
		{
			InitializeComponent();
		}

		private void frmCedula_Load(object sender, EventArgs e)
		{
			try
			{
				//Enumera la Lista de Lectores de Tarjetas conectados al PC:
				if (this.manager.LectoresDisponibles != null && this.manager.LectoresDisponibles.Count > 0)
				{
					foreach (string lector in this.manager.LectoresDisponibles)
					{
						this.comboBox1.Items.Add(lector);
					}
					this.comboBox1.Text = this.manager.LectoresDisponibles[1].ToString();
					this.readerName = this.manager.LectoresDisponibles[1].ToString();

					//[OPCIONAL] Evento que Detecta la insercion de Tarjetas:
					this.manager.OnReaderChangeStatus += new EventHandler(manager_OnReaderChangeStatus);
					this.Delegado1 = OnReaderChangeStatus;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + ex.StackTrace, "Error Inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>Evento que Ocurre al Insertar/Retirar una Tarjeta al lector.</summary>
		/// <param name="sender">Objeto del tipo SmartcardLibrary.SmartcardState que el Estado del Lector.</param>
		/// <param name="e">Null.</param>
		void manager_OnReaderChangeStatus(object sender, EventArgs e)
		{
			if (sender != null)
			{
				SmartcardLibrary.SmartcardState stado = (SmartcardLibrary.SmartcardState)sender;
				//Debido a que la deteccion de Tarjetas ocurre en un Proceso independiente, es necesario usar un delegado
				//para actualizar los controles
				BeginInvoke(this.Delegado1, stado);
			}
		}

		/// <summary>Aqui se responde al Evento 'OnReaderChangeStatus' del Lector de Tarjetas.</summary>
		/// <param name="pState">Objeto del tipo SmartcardLibrary.SmartcardState que el Estado del Lector.</param>
		private void OnReaderChangeStatus(SmartcardState pState)
		{
			try
			{
				switch (pState)
				{
					case SmartcardState.None:
						this.label3.Text = "Lector Listo";
						break;

					case SmartcardState.Inserted:
						//Me Conecto a la Tarjeta para recuperar informacion
						//this.manager.CardConnect(this.readerName);
						this.label3.Text = "Tarjeta Insertada.";
						this.panel1.Visible = false;
						this.panel2.Visible = true;

						Leer_Cedula_WithContact();
						break;

					case SmartcardState.Ejected:
						this.label3.Text = "Tarjeta Retirada";
						this.panel1.Visible = true;
						this.panel2.Visible = false;

						this.lbApellido1.Text = null;
						this.lbApellido2.Text = null;
						this.lbNombres.Text = null;
						this.lbNacionalidad.Text = null;
						this.lbLugarNacimiento.Text = null;
						this.lbFechaNacimiento.Text = null;

						this.lbNroCedula.Text = null;

						this.lbFechaExpedida.Text = null;
						this.lbFechaVence.Text = null;
						this.pictureFoto.Image = null;

						break;

					default:
						break;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + ex.StackTrace, "Error Inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}



		private void Leer_Cedula_WithContact()
		{
			this.Cursor = Cursors.WaitCursor;
			this.readerName = this.comboBox1.Text;

			Delegado2 = Leer_Cedula_Actualizar;            //Esto se dispara cuando se termina de cargar los datos
			MiProceso1 = new Thread(Leer_Cedula_EnAccion); //Este es el proceso nuevo y el Metodo que se ejecuta en el.

			MiProceso1.SetApartmentState(System.Threading.ApartmentState.STA); //<-Permite Llamar cuadros de dialogo
			MiProceso1.Start();

		}

		private void Leer_Cedula_EnAccion()
		{
			try
			{
				if (this.manager != null)
				{					
					this.manager.CardConnect(this.readerName, false);
					APDU_Response _Response = new APDU_Response();

					/* AQUI VAMOS A LEER LOS DATOS DE LA CEDULA URUGUAYA USANDO PARA ELLO UN LECTOR CON CONTACTO
					 * TAMBIEN SE PUEDE CON UN LECTOR 'CONTACTLESS' PERO ES MAS COMPLICADO  */

					//1. SELECT_FILE: Applet IAS Comando= Class=00 Ins=A4 P1=04 P2=00 P3=0C Data= A00000001840000001634200
					byte[] Data = new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x18, 0x40, 0x00, 0x00, 0x01, 0x63, 0x42, 0x00 };
					byte[] _APDU = this.manager.createAPDU_Command(0x00, 0xA4, 0x04, 0x00, (byte)Data.Length, Data);
					_Response = this.manager.CardTransmit(_APDU);
					//_Response:  Ret=00, SW=9000(FF00), Data=00

					//2. SELECT_FILE: Datos_BIO 		00 A4 00 00 02 [70 02] 00   <- [70 02] es el ID del archivo
					_APDU = this.manager.createAPDU_Command(0x00, 0xA4, 0x00, 0x00, 0x02, 0x70, 0x02, 0x00);
					_Response = this.manager.CardTransmit(_APDU);
					// _Response: Ret=00, SW1=61, SW2=xx --> xx es la posicion del FCI Template

					//3. GET_RESPONSE: 00 CO 00 00 xx  <-- (xx es el SW2 del comando anterior)
					_APDU = this.manager.createAPDU_Command(0x00, 0xC0, 0x00, 0x00, _Response.sw2);
					_Response = this.manager.CardTransmit(_APDU);
					/* _Response.SW: 90000(FF00),  Ret=00,
					 * _Response.Data: FCI Template ->  6F 13  81 02 [00 7B] 82 01  01 83  02 70  02 8A  01 05  8C 03 03
					 *   6F <- Tag indicando que esto es un FCI Template
					 *   13 <- Largo total del Template: 13 (hex->Int) = 19 bytes
					 *   81 <- Tag para indicar el Tamaño del Archivo
					 *   02 <- Largo del tag que indica el Tamaño del Archivo (2 bytes)
					 *   [00 7B]  <- Tamaño del Archivo: [007B] = 123 bytes
					 *   [Otros bytes sin relevancia para este caso]  */

					//4. READ_BINARY: Datos_BIO		00 B0 00 00 [xx yy]  <- [xx yy] es el Tamaño del archivo del comando anterior
					_APDU = this.manager.createAPDU_Command(0x00, 0xB0, 0x00, _Response.Data[4], _Response.Data[5]);
					_Response = this.manager.CardTransmit(_APDU);
					/* _Response.SW: 90000(FF00),  Ret=00,
					 * _Response.Data = TLV Template:
					 * 1F 01 0E 43 48 41 43 C3 93 4E 20 52 41 4E 47 45 4C 1F 02 00 1F 03 08 4A 48 4F 4C 4C 
					 * 4D 41 4E 1F 04 03 43 4F 4C 1F 05 08 31 38 30 34 31 39 37 35 1F 06 0D 53 41 4E 54 41 
					 * 4E 44 45 52 2F 43 4F 4C 1F 07 08 35 34 39 36 39 38 34 36 1F 08 04 14 08 20 16 1F 09 
					 * 08 33 30 30 36 32 30 31 39 1F 0A 1B 52 45 53 49 44 45 4E 54 45 20 4C 45 47 41 4C 20 
					 * 44 4E 4D 20 31 31 31 34 2F 30 35 */

					//La clase 'TLV_Cedula' se encarga de decodificar los datos biograficos de la Persona:
					this.Datos_Cedula = new TLV_Cedula(_Response.Data);
					if (this.Datos_Cedula != null) this.BeginInvoke(this.Delegado2, "DatosPersona");


					/**** AHORA VAMOS A LEER LA FOTO, ESTE PROCESO ES ALGO LENTO *****/

					//5. SELECT_FILE: Foto  00 A4 00 00 02 [70 04] 00  <- [70 04] es el ID del archivo
					_APDU = this.manager.createAPDU_Command(0x00, 0xA4, 0x00, 0x00, 0x02, 0x70, 0x04, 0x00);
					_Response = this.manager.CardTransmit(_APDU);
					// _Response: Ret=00, SW1=61, SW2=xx --> xx es la posicion del FCI Template

					//6. GET_RESPONSE: 00 CO 00 00 xx <-- (xx es el SW2 del comando anterior)
					_APDU = this.manager.createAPDU_Command(0x00, 0xC0, 0x00, 0x00, _Response.sw2);
					_Response = this.manager.CardTransmit(_APDU);
					/* _Response.SW: 90000(FF00),  Ret=00,
					 * _Response.Data: FCI Template =
					 *    6F 13 81 02 [25 B9] 82 01 01 83 02 70 04 8A 01 05 8C 03 03 [FF 00]  
					 *    0  1  2  3   4  5   6  7  .. .. 
					 *  [25 B9] = 9657 bytes => Tamaño del FCI Template que contiene la imagen 
					 *  [Otros bytes sin relevancia para este caso] 			 */

					//7. Obtengo el Tamaño esperado de todo el Archivo que contiene la Imagen (incluye 5 bytes del Tag):
					int foto_len = int.Parse(this.manager.byteToHexa(
						new byte[] { _Response.Data[4], _Response.Data[5] }, 2, false), System.Globalization.NumberStyles.HexNumber);
					int nro_corridas = (foto_len / 255) + 1;    //<- Numero de corridas necesarias para recuperar toda la Foto
					byte[] foto_bytes = new byte[foto_len];     //<- Buffer para almacenar la Foto	

					//1. Recupero los primeros 255 bytes, incluye 5 bytes del FCI Template
					//READ_BINARY FotoPersona:  00 B0 00 00 FF  <- P1 y P2 son el Offset donde se va leyendo el archivo, en este caso [00 00]
					_APDU = this.manager.createAPDU_Command(0x00, 0xB0, 0x00, 0x00, 0xFF);
					_Response = this.manager.CardTransmit(_APDU);
					/*  _Response.SW: 90000(FF00), Ret=00,
					 *  _Response.Data:  FCI Template + Foto
					 *  3F 01 <- Tag
					 *  82    <- Tamaño en 2 bytes
						25 B4 <- Tamaño de la Imagen = 9.652 bytes
						[3F 01 82 25 B4] FF D8 FF E0 00 10 4A 46 49 46 00 01 01 00 00 01 00 01 00 00 FF DB 00 43 00 08 06 06 07 06 05 08 07 07 07 
						09 09 08 0A 0C 14 0D 0C 0B 0B 0C 19 12 13 0F 14 1D 1A 1F 1E 1D 1A 1C 1C 20 24 2E 27 20 22 2C 23 1C 1C 28 37 29 2C 30 31 
						34 34 34 1F 27 39 3D 38 32 3C 2E 33 34 32 FF DB 00 43 01 09 09 09 0C 0B 0C 18 0D 0D 18 32 21 1C 21 32 32 32 32 32 32 32 
						32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 32 
						32 32 32 FF C0 00 11 08 01 40 00 F0 03 01 22 00 02 11 01 03 11 01 FF C4 00 1F 00 00 01 05 01 01 01 01 01 01 00 00 00 00 
						00 00 00 00 01 02 03 04 05 06 07 08 09 0A 0B FF C4 00 B5 10 00 02 01 03 03 02 04 03 05 05 04 04 00 00 01 7D 01 02 03 00 
						04 11 05 12 21 31 41 06 13 51 61 07 22 71 14 00 */

					//Voy guardando los bytes recuperados en el Buffer de la Foto:
					if (_Response.is_ok && _Response.has_data) Array.Copy(_Response.Data, 0, foto_bytes, 0, _Response.Data.Length);

					byte _P2 = 0xFF; //<- Incremento el Offset en 255 ('FF'), 
					int ult_ix = _Response.Data.Length; //<- Posicion del Ultimo(+1) Byte Recuperado

					//Ahora a extraer el resto del archivo:
					//Se hace asi porque solo podemos extraer 255 bytes cada vez
					/*	CLA INS P1 P2 Le
						----------------
						00   B0 00 FF FF	<- Siguientes 255 bytes (no hay tag)
						00   B0 01 FE FF
						00   B0 02 FD FF
						00   B0 03 FC FF
						00   B0 04 FB FF
						..   .. .. .. ..  <- Hasta Terminar 
					*/
					for (int _P1 = 0; _P1 <= nro_corridas; _P1++)
					{
						//READ_BINARY FotoPersona:
						_APDU = this.manager.createAPDU_Command(0x00, 0xB0, (byte)_P1, _P2, 0xFF);
						_Response = this.manager.CardTransmit(_APDU);
						/* _Response.SW: 90000(FF00), Ret=00,
					     * _Response.Data: [Bytes de la foto]    */

						if (_Response.is_ok && _Response.has_data)
						{
							//Voy guardando los bytes recuperados en el Buffer de la Foto:
							Array.Copy(_Response.Data, 0, foto_bytes, ult_ix, _Response.Data.Length);

							_P2--;  //<- vamos reduciendo el Offset para la siguiente corrida
							ult_ix += _Response.Data.Length; //<- Posicion del Ultimo(+1) Byte Recuperado
						}
					}

					// Quitar los 5 bytes del Tag FCI Template y Guardar la Imagen en un Archivo JPG:
					byte[] foto_ = new byte[foto_bytes.Length - 5];
					Array.Copy(foto_bytes, 5, foto_, 0, foto_bytes.Length - 5);

					//Adjunto la foto a los demas datos de la Persona:
					this.Datos_Cedula.foto_persona = foto_;
					this.BeginInvoke(this.Delegado2, "Foto");

					using (System.Drawing.Image image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(foto_)))
					{
						image.Save(@"C:\Temp\foto2.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
						if (System.IO.File.Exists(@"C:\Temp\foto2.jpg"))
						{
							//abre con el programa predeterminado:
							System.Diagnostics.Process.Start(@"C:\Temp\foto2.jpg");
						}
					}
				}
			}
			catch (ThreadAbortException)
			{
				MiProceso1.Join();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + ex.StackTrace, "Error Inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				this.BeginInvoke(this.Delegado2, "Terminar");
			}
		}

		private void Leer_Cedula_Actualizar(string pMensaje)
		{
			//Aqui se reciben los mensajes del Proceso1
			//y se actualizan los controles
			switch (pMensaje)
			{
				case "DatosPersona":
					if (this.Datos_Cedula != null)
					{
						this.lbApellido1.Text = this.Datos_Cedula.apellido_1;
						this.lbApellido2.Text = this.Datos_Cedula.apellido_2;
						this.lbNombres.Text = this.Datos_Cedula.nombres;
						this.lbNacionalidad.Text = this.Datos_Cedula.nacionalidad;
						this.lbLugarNacimiento.Text = this.Datos_Cedula.lugar_nacimiento;
						this.lbFechaNacimiento.Text = this.Datos_Cedula.fecha_nacimiento.ToShortDateString();

						this.lbNroCedula.Text = FormatearCI(this.Datos_Cedula.nro_cedula);

						this.lbFechaExpedida.Text = this.Datos_Cedula.fecha_expedicion.ToShortDateString();
						this.lbFechaVence.Text = this.Datos_Cedula.fecha_vencimiento.ToShortDateString();
					}
					break;

				case "Foto":
					if (this.Datos_Cedula != null && this.Datos_Cedula.foto_persona != null)
					{
						this.pictureFoto.Image = ByteArrayToImage(this.Datos_Cedula.foto_persona);
					}
					break;

				case "Terminar":
					this.Cursor = Cursors.Default;
					break;

				default:
					break;
			}
		}

		public Image ByteArrayToImage(byte[] bytesArr)
		{
			using (System.IO.MemoryStream memstr = new System.IO.MemoryStream(bytesArr))
			{
				Image img = Image.FromStream(memstr);
				return img;
			}
		}

		/// <summary>Formatea Numeros de Cedula, Internos y Coches.</summary>
		/// <param name="pCedula">Numero de Documento a formatear.</param>
		public static string FormatearCI(string pDocumento)
		{
			string v_formateado = string.Empty;
			int posicion = 0;
			int largo = 0;
			int largo2 = 0;

			largo = pDocumento.Length - 1;
			largo2 = pDocumento.Length;

			for (posicion = 0; posicion <= largo; posicion++)
			{
				if (posicion == 1)
				{
					v_formateado = "-" + v_formateado;
				}
				if (posicion == 4)
				{
					v_formateado = "." + v_formateado;
				}
				if (posicion == 7)
				{
					v_formateado = "." + v_formateado;
				}

				v_formateado = pDocumento.Substring(largo - posicion, 1) + v_formateado;
			}
			return v_formateado;
		}

	}
}
