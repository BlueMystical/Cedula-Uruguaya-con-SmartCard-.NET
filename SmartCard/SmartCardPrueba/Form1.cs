using System;
using System.Windows.Forms;
using SmartcardLibrary;

namespace SmartCardPrueba
{
	public partial class Form1 : Form
	{
		private SmartcardManager manager = SmartcardManager.GetManager(); //<-Representa al Lector de Tarjetas		
		private string readerName = string.Empty; //<- Nombre del Lector de Tarjetas Seleccionado

		//[OPCIONAL] Delegado para Detectar la Insercion/Retiro de Tarjetas del lector
		private delegate void OnReaderChangeStatus_CallBack(SmartcardState pState);
		private OnReaderChangeStatus_CallBack Delegado1;

		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			try
			{
				//Enumera la Lista de Lectores de Tarjetas conectados al PC:
				//List<string> lectores = manager.ListReaders();
				if (this.manager.LectoresDisponibles != null && this.manager.LectoresDisponibles.Count > 0)
				{
					foreach (string lector in this.manager.LectoresDisponibles)
					{
						this.comboBox1.Items.Add(lector);
					}
					this.comboBox1.Text = this.manager.LectoresDisponibles[0].ToString();
					this.readerName = this.manager.LectoresDisponibles[0].ToString();
					this.listBox1.Items.Add("Lector Listo");
				}
				//[OPCIONAL] Evento que Detecta la insercion de Tarjetas:
				this.manager.OnReaderChangeStatus += new EventHandler(manager_OnReaderChangeStatus);
				this.Delegado1 = OnReaderChangeStatus;
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
						this.listBox1.Items.Add("Lector Listo");
						break;

					case SmartcardState.Inserted:
						//Me Conecto a la Tarjeta para recuperar informacion
						this.manager.CardConnect(this.readerName);
						if (!this.manager.IsPresentCard()) //<-Reviso si aún está presente la tarjeta
						{
							this.listBox1.Items.Add("Tarjeta No Detectada.");
						}
						else
						{
							this.listBox1.Items.Add("Tarjeta Insertada:");

							//El UID en formato Hexadecimal (Real).
							//Cutcsa(Fase II) reconoce los mismos codigos pero invertidos,
							//Esto se debe a que Cutcsa sólo reconoce el Formato Entero(Int32).
							//Los Pases Libres hay que invertirlos

							//El UID en formato Long(Int64) vendría a ser el valor Real del UID:
							string hexNor = this.manager.GetCardUID(true, false).Replace(" ", "");
							Int64 UID_nor = Int64.Parse(hexNor, System.Globalization.NumberStyles.HexNumber);

							this.listBox1.Items.Add(string.Format("	ATR: {0}", this.manager.ATR[0]));
							this.listBox1.Items.Add(string.Format("	{0}", this.manager.ATR[1]));
							if (this.manager.ATR.Count > 2) this.listBox1.Items.Add(string.Format("	{0}", this.manager.ATR[2]));
							if (this.manager.ATR.Count > 3) this.listBox1.Items.Add(string.Format("	{0}", this.manager.ATR[3]));

							this.listBox1.Items.Add(string.Format("	UID Real -> {0} = {1}", hexNor, UID_nor));

							//El UID en formato Int(Int32) es el valor reconocido x Cutcsa (pases libres):
							string hexInv = this.manager.GetCardUID(true, true).Replace(" ", "");
							Int32 UID_inv = Int32.Parse(hexInv, System.Globalization.NumberStyles.HexNumber);
							//Se debe a que quien programo originalmente los pases libres se equivoco al hacer la conversion de Hex a Entero
							//Usó Int32 envez de usar Int64, x lo cual el valor real seria este:
							UID_nor = Int64.Parse(hexInv, System.Globalization.NumberStyles.HexNumber);

							this.listBox1.Items.Add(string.Format("	UID Inv.  -> {0} = {1} -> Cutcsa: {2}", hexInv, UID_nor, UID_inv));
						}
						break;

					case SmartcardState.Ejected:
						this.listBox1.Items.Add("Tarjeta Retirada");
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

		private void button1_Click(object sender, EventArgs e)
		{
			try
			{
				frmCedula Cedula = new frmCedula();
				Cedula.Show();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + ex.StackTrace, "Error Inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			try
			{
				if (this.manager != null)
				{
					this.manager.CardConnect(this.readerName, false);

					APDU_Response _Response = new APDU_Response();

					//1. Select Applet EF_CardAccess: Comando= Class=00 Ins=A4 P1=04 P2=00 P3=0C Data= A00000001840000001634200
					byte[] Data = new byte[] { 0x01, 0x1E };

					//00 A4 00 00 00 <- MAIN FILE
					byte[] _APDU = this.manager.createAPDU_Command(0x00, 0xA4, 0x04, 0x00, (byte)Data.Length, Data);
					//_APDU = this.manager.createAPDU_Command(0x00, 0xA4, 0x04, 0x00, 0x00, 0x00);
					//_Response = this.manager.CardTransmit(_APDU);

					//if (_Response.is_ok)
					//{
					//	/* 6F 64 84 07 A0 00 00 00 18 43 4D A5 59 73 4A 06 07 2A 86 48 86 FC 6B 01 60 0C 06 0A 2A 86 48 86 FC 
					//	 * 6B 02 02 01 01 63 09 06 07 2A 86 48 86 FC 6B 03 64 0B 06 09 2A 86 48 86 FC 6B 04 02 55 65 0B 06 09 
					//	 * 2B 85 10 86 48 64 02 01 03 66 0C 06 0A 2B 06 01 04 01 2A 02 6E 01 02 9F 6E 06 12 91 21 72 03 00 9F 
					//	 * 65 01 FF [90 00] 
					//	 */
					//}

					/* 
					 * 1. Select - select the epassport application
					 * 2. Get Challenge 
					 * 3. External Authenticate
					 * 4. Read Binary(protected by secure messaging) - read the DG1 file which contains basic passport holder info
					 * 5. Read Binary(protected by secure messaging) - read the DG2 file which contains the photo of the passport holder info
					*/


					//1. Select App ePassport MRZ: "04", "0C", "A0 00 00 02 47 10 01"
					Data = new byte[] { 0xA0, 0x00, 0x00, 0x02, 0x47, 0x10, 0x01 };
					_APDU = this.manager.createAPDU_Command(0x00, 0xA4, 0x04, 0x0C, (byte)Data.Length, Data);
					_Response = this.manager.CardTransmit(_APDU);
					//[90 00]
					
					if (_Response.is_ok)
					{
						//2. Get Challenge :
						_APDU = this.manager.createAPDU_Command(0x00, 0x84, 0x00, 0x00, 0x08);
						_Response = this.manager.CardTransmit(_APDU);
						// D7 FB D7 CE 84 C3 CD 09 [90 00] 

						if (_Response.is_ok)
						{
							//3. EXTERNAL AUTENTICATE:
							Data = _Response.Data;
							_APDU = this.manager.createAPDU_Command(0x00, 0x82, 0x00, 0x00, (byte)Data.Length, Data);
							_Response = this.manager.CardTransmit(_APDU);

							if (_Response.is_ok)
							{

							}
						}
						else
						{
							MessageBox.Show(_Response.ToString(), "Error Inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
						}
					}

				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message + ex.StackTrace, "Error Inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}
