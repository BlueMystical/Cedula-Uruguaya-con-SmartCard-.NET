using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace SmartcardLibrary
{
	/// <summary>Codigos para el Estado de una Tarjeta en el Lector.</summary>
	public enum SmartcardState
	{
		None = 0,
		Inserted = 1,
		Ejected = 2
	}

	//https://www.openscdp.org/scripts/tutorial/emv/reademv.html
	//https://centroderecursos.agesic.gub.uy/web/seguridad/wiki/-/wiki/Main/Gu%C3%ADa+de+uso+de+CI+electr%C3%B3nica+a+trav%C3%A9s+de+APDU


	/*COMANDOS:
	 * SELECT_FILE:		00 A4 00 00 00 (Master File if available)
	 * GET_RESPONSE:	00 CO 00 00 xx (PROTOCOL T=0 ONLY)
	 * READ_RECORD:		00 B2 01 1C xx
	 * GET DATA:		FF CA F1 00 00 <- Card info
	 * READ_BINARY:		FF B0 00 BLOCK Le  <- Block:0-63
	 * UPDATE BINARY:	FF D6 00 BLOCK Lc DATA
	 * WRITE_FILE:		FF F4 00 BLOCK Lc DATA
	 * LOAD_KEY:		FF 82 00 00 06 FF FF FF FF FF FF
	 * AUTENTICATE:		FF 86 00 00 05 DATA  <- 01 00 04 60 00
	 * ESCAPE_COMMANDS:	FF CC 00 00 Lc DATA
	 *		DATA:	0x11 <- READER_GETCARDINFO
	 * */

	/// <summary>Clase Encargada de Manejar todas las comunicacion hacia y desde un Lector de Tarjetas.</summary>
	public class SmartcardManager : IDisposable
	{
		#region Member Fields

		//Shared members are lazily initialized.
		//.NET guarantees thread safety for shared initialization.
		private static readonly SmartcardManager _instance = new SmartcardManager();

		private bool _disposed = false;
		private ReaderState[] _states;
		private SmartcardState _state;
		private SmartcardErrorCode _lastErrorCode;

		//A thread that watches for new smart cards.
		private BackgroundWorker _Bworker;

		private IntPtr _ReaderContext = IntPtr.Zero;
		private IntPtr _CardContext = IntPtr.Zero;
		private IntPtr ActiveProtocol = IntPtr.Zero;

		private string _szReaderName = string.Empty;
		private List<string> mAvailableReaders = null;

		private int _lMode = 2;
		private int _lProtocol = 3;
		private int _lDisconnectOption;

		#endregion

		#region Constructor
		//Make the constructor private to hide it. This class adheres to the singleton pattern.
		private SmartcardManager()
		{
			//Create a new SafeHandle to store the smartcard context.
			this._ReaderContext = IntPtr.Zero;

			EstablishContext(); //<- Establish a context with the PC/SC resource manager.
			if (this.HasContext)
			{
				//Compose a list of the card readers which are connected to the system and which will be monitored.
				this.mAvailableReaders = ListReaders();
				this._states = new ReaderState[this.mAvailableReaders.Count];
				for (int i = 0; i <= this.mAvailableReaders.Count - 1; i++)
				{
					this._states[i].Reader = this.mAvailableReaders[i].ToString();
				}

				//Start a background worker thread which monitors the specified
				//card readers.
				if ((this.mAvailableReaders.Count > 0))
				{
					this._Bworker = new BackgroundWorker();
					this._Bworker.WorkerSupportsCancellation = true;
					this._Bworker.WorkerReportsProgress = true;
					this._Bworker.DoWork += WaitChangeStatus;
					this._Bworker.ProgressChanged += new ProgressChangedEventHandler(_worker_ProgressChanged);
					this._Bworker.RunWorkerAsync(this._state);
				}
			}
		}

		/// <summary>Devuelve una Instancia de la Clase SmartcardManager e inicaliza los procesos (Constructor).</summary>
		public static SmartcardManager GetManager()
		{
			return _instance;
		}

		#endregion

		#region Eventos

		/// <summary>Evento que se produce al Cambiar el Estado del Lector, ejem: Al insertar o retirar una Tarjeta.
		/// Este Evento se Ejecuta en un Sub-Proceso Independiente, usar un Delegado para actualizar controles.</summary>
		public event EventHandler OnReaderChangeStatus;

		private void WaitChangeStatus(object sender, DoWorkEventArgs e)
		{
			while (!e.Cancel)
			{
				SmartcardErrorCode result = SmartcardErrorCode.None;

				//Obtain a lock when we use the context pointer, 
				//which may be modified in the Dispose() method.

				lock (this)
				{
					if (!this.HasContext)
					{
						return;
					}

					//This thread will be executed every 1000ms. 
					//The thread also blocks for 1000ms, meaning 
					//that the application may keep on running for 
					//one extra second after the user has closed 
					//the Main Form.
					if (this._states != null)
					{
						result = (SmartcardErrorCode)UnsafeNativeMethods.GetStatusChange(
								this._ReaderContext, 1000, this._states, this._states.Length);
					}
				}

				if ((result == SmartcardErrorCode.Timeout))
				{
					// Time out has passed, but there is no new info. Just go on with the loop
					continue;
				}
				if (this._states != null)
				{
					for (int i = 0; i <= this._states.Length - 1; i++)
					{
						//Check if the state changed from the last time.
						if ((this._states[i].EventState & CardState.Changed) == CardState.Changed)
						{
							//Check what changed.
							this._state = SmartcardState.None;
							if ((this._states[i].EventState & CardState.Present) == CardState.Present
								&& (this._states[i].CurrentState & CardState.Present) != CardState.Present)
							{
								//The card was inserted.                            
								this._state = SmartcardState.Inserted;
							}
							else if ((this._states[i].EventState & CardState.Empty) == CardState.Empty
								&& (this._states[i].CurrentState & CardState.Empty) != CardState.Empty)
							{
								//The card was ejected.
								this._state = SmartcardState.Ejected;
							}
							if (this._state != SmartcardState.None && this._states[i].CurrentState != CardState.None)
							{
								//Aqui Envia informacion para Generar el evento 'OnReaderChangeStatus' fuera del proceso actual
								this._Bworker.ReportProgress(0, this._state);
							}
							//Update the current state for the next time they are checked.
							this._states[i].CurrentState = this._states[i].EventState;
						}
					}
				}
			}
		}

		private void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			this.OnReaderChangeStatus(this._state, null);
		}

		#endregion

		#region Methods

		private bool EstablishContext()
		{
			if ((this.HasContext))
			{
				//this._ReaderContext = IntPtr.Zero;
				return true;
			}

			this._lastErrorCode = (SmartcardErrorCode)UnsafeNativeMethods.SCardEstablishContext(
						2,
						IntPtr.Zero,
						IntPtr.Zero,
						out this._ReaderContext
				);
			return (this._lastErrorCode == SmartcardErrorCode.None);
		}

		#region Metodos Publicos

		/// <summary>Devuelve una Lista con los Nombres de los Lectores de Tarjetas Conectados al PC.</summary>
		public List<string> ListReaders()
		{
			this.mAvailableReaders = new List<string>();

			//Make sure a context has been established before 
			//retrieving the list of smartcard readers.
			if (this.HasContext)
			{
				uint pcchReaders = 0;
				int nullindex = -1;
				char nullchar = (char)0;

				// First call with 3rd parameter set to null gets readers buffer length.
				int _result = UnsafeNativeMethods.SCardListReaders(this._ReaderContext, null, null, ref pcchReaders);
				if (_result == 0)
				{
					byte[] mszReaders = new byte[pcchReaders];

					// Fill readers buffer with second call.
					_result = UnsafeNativeMethods.SCardListReaders(this._ReaderContext, null, mszReaders, ref pcchReaders);
					if (_result == 0)
					{

						// Populate List with readers.
						string currbuff = new ASCIIEncoding().GetString(mszReaders);
						int len = (int)pcchReaders;

						if (len > 0)
						{
							while (currbuff[0] != nullchar)
							{
								nullindex = currbuff.IndexOf(nullchar);   // Get null end character.
								string reader = currbuff.Substring(0, nullindex);
								this.mAvailableReaders.Add(reader);
								len = len - (reader.Length + 1);
								currbuff = currbuff.Substring(nullindex + 1, len);
							}
						}
					}
				}
				else
				{
					//throw new Exception(
					//		string.Format("Problemas al conectar con el Lector, Error: {0} {1}",
					//		System.Convert.ToString(_result, 16), ((SCardFunctionReturnCodes)_result).ToString())
					//	);
				}
			}
			return this.mAvailableReaders;
		}

		/// <summary>Se Conecta a la Tarjeta actualmente Presente en el Dispositivo Lector Especificado.</summary>
		/// <param name="pReaderName">Nombre del Dispositivo Lector de Tarjetas.</param>
		public bool CardConnect(string pReaderName, bool getATR = true)
		{
			bool Ret = false;
			if (this._ReaderContext != IntPtr.Zero)
			{
				this._szReaderName = pReaderName;

				long _ret = UnsafeNativeMethods.SCardConnect(this._ReaderContext, this._szReaderName, 0x00000002, 0x00000001, ref this._CardContext, ref this.ActiveProtocol);
				if (_ret == 0)
				{
					if (getATR) this.ATR = Get_ATRinfo();
					Ret = true;
				}
				else
				{
					//throw new Exception(
					//		string.Format("Problemas al conectar con la tarjeta, Error: {0} {1}",
					//		System.Convert.ToString(_ret, 16), ((SCardFunctionReturnCodes)_ret).ToString())
					//	);
				}
			}
			return Ret;
		}

		/// <summary>Desconecta de manera Segura la Tarjeta del Lector.</summary>
		public void CardDisconnect()
		{
			if (this._CardContext != IntPtr.Zero)
			{
				this._lDisconnectOption = 3;
				int _ret = UnsafeNativeMethods.SCardDisconnect(this._CardContext, this._lDisconnectOption);
				if (_ret != 0)
				{
					//throw new Exception(
					//	string.Format("Problemas al desconectar de la tarjeta Error: {0} {1}",
					//	System.Convert.ToString(_ret, 16), ((SCardFunctionReturnCodes)_ret).ToString())
					//);
				}
				this._CardContext = IntPtr.Zero;
				this.ATR = null;
			}
		}
			   
		/// <summary>Verifica la Presencia de una Tarjeta en el Dispositivo Lector.</summary>
		public bool IsPresentCard()
		{
			int num1 = 0;
			int num3 = 0;
			string text1 = string.Empty;
			byte[] buffer1 = new byte[32];
			string cadena = string.Empty;

			text1.PadRight(256, ' ');
			int largoCadena = cadena.Length;

			int retorno = UnsafeNativeMethods.SCardStatus(this._CardContext, ref cadena, ref largoCadena, ref num3, ref this._lProtocol, buffer1, ref num1);
			if (retorno != 0)
			{
				return false;
			}
			return ((num3 == 2) | (num3 == 6));
		}

		/// <summary>Envia un Comando APDU a la tarjeta y devuelve su Respuesta, todo en array de Bytes.</summary>
		/// <param name="apdu_command">Array con el Comando, ejem:  byte[] sendBytes = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };</param>
		public APDU_Response CardTransmit(byte[] apdu_command)
		{
			byte[] dataOut = new byte[258]; //<-255 + 2 del Trailer (SW1,SW2)
			int sendLen = apdu_command.Length;
			uint ret_Len = (uint)dataOut.Length;

			UnsafeNativeMethods.SCARD_IO_REQUEST request = new UnsafeNativeMethods.SCARD_IO_REQUEST()
			{
				dwProtocol = (uint)this.ActiveProtocol,
				cbPciLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(UnsafeNativeMethods.SCARD_IO_REQUEST))
			};

			int ret = UnsafeNativeMethods.SCardTransmit(this._CardContext, ref request, ref apdu_command[0], (uint)sendLen, ref request, dataOut, ref ret_Len);
			return new APDU_Response(dataOut, ret, (int)ret_Len);
		}


		/// <summary>Obtiene el codigo ATR de la Tarjeta y extrae informacion basica sobre ella.</summary>
		public List<string> Get_ATRinfo()
		{
			List<string> _ATR = null;
			IntPtr _CardContext = IntPtr.Zero;
			IntPtr ActiveProtocol = IntPtr.Zero;

			try
			{
				int _result = 0;
				//Debe haber un lector conectado
				if (this._szReaderName != string.Empty && this._ReaderContext != IntPtr.Zero)
				{
					//Conectar con la Tarjeta
					if (_CardContext == IntPtr.Zero)
					{
						_result = UnsafeNativeMethods.SCardConnect(this._ReaderContext, this._szReaderName, 0x00000002, 0x00000001, ref _CardContext, ref ActiveProtocol);
					}

					if (_CardContext != IntPtr.Zero)
					{
						byte[] pcbAttr = new byte[20];
						IntPtr pcbAttrLen = new IntPtr(pcbAttr.Length);
						const UInt32 SCARD_ATTR_ATR_STRING = 0x00090303;

						//Obtiene el ATR de la tarjeta
						_result = UnsafeNativeMethods.SCardGetAttrib(_CardContext, SCARD_ATTR_ATR_STRING, pcbAttr, ref pcbAttrLen);
						APDU_Response _RESPONSE = new APDU_Response(pcbAttr, _result, (int)pcbAttrLen, false);

						if (_RESPONSE != null && _RESPONSE.Return.code == 0)
						{	

							_ATR = new List<string>();
							_ATR.Add(_RESPONSE.Data_HexString);  //byteToHexa(Data, 20, true)); //0

							/*Answer - To - Reset(ATR)
							Hdr | T0 | TD1|TD2 | T1 | Tk | Len| RID            | Std| Card  | RFU         | TCK |
							3B  | 8F | 80 | 01 | 80 | 4F | 0C | A0 00 00 03 06 | 03 | 00 03 | 00 00 00 00 | 68
							0     1    2    3    4    5    6    7  8  9  10 11   12   13 14   15 16 17 18   19
							3B    8C   80   01   50   00   05   70 3B 00 00 00   00   33 81   81 20 
							3B    8F   80   01   80   4F   0C   A0 00 00 03 06   03   F0 11   00 00 00 00   8A 
							3B    88   80   01   E1   F3   5E   11 77 87 95 00   31   00 00   00 00 00 00   00		<- Cedula Uruguaya
							3B    8F   80   01   80   4F   0C   A0 00 00 03 06   03   00 01   00 00 00 
							3B    8F   80   01   80   4F   0C   A0 00 00 03 06   03   00 01   00 00 00 00   6A
																A0 00 00 00 03
																
							
							Worth studying are the following fields:
							Tk = Application Identifier Presence Indicator
							Len = 0C              // 12 bytes of config data
							RID = A0 00 00 03 06  // Registered App Provider Identifier: PC/SC Workgroup
							Std = 03              // Standard format: RFID - ISO 14443 Type A Part 3
							Card Name = 00 03     // Mifare Ultralight*/

							string[] atr_Bytes = _ATR[0].Split(new char[] { ' ' });

							//1ª Linea: Fabricante o Distribuidor
							string rid = string.Format("{0} {1} {2} {3} {4}", atr_Bytes[7], atr_Bytes[8], atr_Bytes[9], atr_Bytes[10], atr_Bytes[11]);
							switch (rid) 
							{
								case "A0 00 00 03 06": _ATR.Add("PC/SC Workgroup"); break;
								case "A0 00 00 00 03": _ATR.Add("Visa"); break;
								case "A0 00 00 00 04": _ATR.Add("MasterCard"); break;
								case "A0 00 00 00 05": _ATR.Add("MasterCard Maestro UK"); break;
								case "A0 00 00 00 25": _ATR.Add("American Express"); break;
								case "A0 00 00 00 65": _ATR.Add("Japan Credit Bureau (JCB)"); break;
								case "A0 00 00 01 52": _ATR.Add("Diners Club/Discover"); break;
								case "A0 00 00 02 77": _ATR.Add("Interac (Canada) Debit card"); break;
								case "A0 00 00 03 33": _ATR.Add("UnionPay"); break;
								case "A0 00 00 05 24": _ATR.Add("RuPay (India)"); break;
								case "70 3B 00 00 00": _ATR.Add("DESFIRE Tag"); break;
								case "11 77 87 95 00": _ATR.Add("Electronic Identification Card (eID)"); break;
								default:
									break;
							}

							//2ª Linea: TIPO DE TARJETA
							string card_name = string.Format("{0} {1}", atr_Bytes[13], atr_Bytes[14]);
							switch (card_name) 
							{
								case "00 00": _ATR.Add("No Card information given."); break;
								case "00 01": _ATR.Add("Mifare Classic 1K"); break;
								case "00 02": _ATR.Add("Mifare 4k"); break;
								case "00 03": _ATR.Add("Mifare Ultralight"); break;
								case "00 06": _ATR.Add("ST MicroElectronics SR176"); break;
								case "00 07": _ATR.Add("ST MicroElectronics SRI4K, SRIX4K, SRIX512, SRI512, SRT512"); break;

								case "00 0A": _ATR.Add("Atmel AT88SC0808CRF"); break;
								case "00 0B": _ATR.Add("Atmel AT88SC1616CRF"); break;

								case "00 12": _ATR.Add("Texas Intruments TAG IT"); break;
								case "00 13": _ATR.Add("ST MicroElectronics LRI512"); break;
								case "00 14": _ATR.Add("NXP ICODE SLI"); break;
								case "00 16": _ATR.Add("NXP ICODE1"); break;
								case "00 21": _ATR.Add("ST MicroElectronics LRI64"); break;
								case "00 23": _ATR.Add("NXP ICODE ILT-M"); break;
								case "00 24": _ATR.Add("ST MicroElectronics LR12"); break;
								case "00 25": _ATR.Add("ST MicroElectronics LRI128"); break;
								case "00 26": _ATR.Add("NXP Mifare Mini"); break;
								case "00 2F": _ATR.Add("Innovision Jewel"); break;
								case "00 30": _ATR.Add("Innovision Topaz (NFC Forum type 1 tag)"); break;
								case "00 34": _ATR.Add("Atmel AT88RF04C"); break;
								case "00 35": _ATR.Add("NXP ICODE SL2"); break;					
								case "00 36": _ATR.Add("Mifare Plus 2K - SL1"); break;
								case "00 37": _ATR.Add("Mifare Plus 4K - SL1"); break;
								case "00 38": _ATR.Add("Mifare Plus 2K - SL2"); break;
								case "00 39": _ATR.Add("Mifare 4K - SL2"); break;

								case "00 3A": _ATR.Add("Mifare Ultralight C"); break;

								case "FF A0": _ATR.Add("Generic/unknown 14443-A card"); break;
								case "FF B0": _ATR.Add("Generic/unknown 14443-B card"); break;
								case "FF B1": _ATR.Add("ASK CTS 256B"); break;
								case "FF B2": _ATR.Add("ASK CTS 512B"); break;
								case "FF B7": _ATR.Add("Inside Contactless PICOTAG/PICOPASS"); break;
								case "FF B8": _ATR.Add("Unidentified Atmel AT88SC / AT88RF card"); break;
								case "FF C0": _ATR.Add("Calypso card using the Innovatron protocol"); break;
								case "FF D0": _ATR.Add("Unidentified ISO 15693 from unknown manufacturer"); break;
								case "FF D1": _ATR.Add("Unidentified ISO 15693 from EMMarin (or Legic)"); break;
								case "FF D2": _ATR.Add("Unidentified ISO 15693 from ST MicroElectronics"); break;

								case "FF FF": _ATR.Add("Virtual card (test only)"); break;

								case "F0 04": _ATR.Add("Topaz and Jewel"); break;
								case "F0 11": _ATR.Add("Felica 212k"); break;
								case "F0 12": _ATR.Add("Felica 424k"); break;

								default:
									_ATR.Add("Unknown Card"); break;
							}

							//3ª Linea: FORMATO DE LOS DATOS ALMACENADOS
							string std_format = atr_Bytes[12];
							switch (std_format)
							{
								case "00": _ATR.Add("ISO 14443 - 4 Type B tag"); break;
								case "03": _ATR.Add("Contacless ISO/IEC 14443 (13,56Mhz) Type A Part 3"); break;
								case "11": _ATR.Add("FeliCa Tags (ISO 18092)"); break;
								case "31": _ATR.Add("IS0 7816 ICAO-compliant"); break;

								//Contact smart card ISO/IEC 7816 
								//ISO/IEC 10536 (3-5Mhz)
								//Hand Free Cards ISO/IEC 15693

								default:
									break;
							}
						}
						else
						{
							throw new Exception(string.Format("ERR: {0}", _RESPONSE.Return.descripcion));
						}
					}
					else
					{
						throw new Exception("No hay Tarjeta en el Lector!");
					}
				}
			}
			catch { }
			this.ATR = _ATR;
			return _ATR;
		}

		/// <summary>Devuelve una cadena con el UID de la Tarjeta en formato Hexadecimal y Entero, (HEX-INT).</summary>
		/// <param name="pHexReturn">Indica el tipo de dato que retorna la funcion: 'true'=Hexadecimal, 'false'=Entero.</param>
		public string GetCardUID(bool pHexReturn = false)
		{
			return GetCardUID(pHexReturn, false);       //<- sin invertir
		}

		/// <summary>Devuelve una cadena con el UID de la Tarjeta en formato Hexadecimal y Entero, (HEX-INT).</summary>
		/// <param name="pHexReturn">Indica el tipo de dato que retorna la funcion: 'true'=Hexadecimal, 'false'=Entero.</param>
		/// <param name="pInvierte">Indica si devuelve en el orden que se lee o devuelve habiendo invertido de derecha a izquierda los valores Hexadecimales.
		///		<para>EN EL CASO DE QUE INVIERTA, devuelve el valor Entero convirtiendolo a Int32.</para>
		///		<para>EN EL CASO DE QUE NO INVIERTA, devuelve el valor Entero convirtiendolo a Int64</para></param> 
		public string GetCardUID(bool pHexReturn, bool pInvierte)
		{
			string ret = string.Empty;
			try
			{
				//Crea el Comando para Obtener el UID de la Tarjeta:
				byte[] sendBytes = createAPDU_Command(0xFF, 0xCA, 0x00, 0x00, 0x00);

				//Enviar el Comando a la Tarjeta:
				APDU_Response ret_Bytes = CardTransmit(sendBytes);
				if (ret_Bytes.is_ok)
				{
					//Obtiene el Codigo Hexadecimal del UID:
					byte[] UID = ret_Bytes.Data; 	

					string hexCode = byteToHexa(UID, UID.Length, true).Trim(); //<- Convertir la respuesta en Hexadecimal
					string[] aux = hexCode.Split(' ');

					if (pInvierte)
						hexCode = aux[3] + aux[2] + aux[1] + aux[0];
					else
						hexCode = aux[0] + aux[1] + aux[2] + aux[3];

					//Devuelve una cadena con el UID en el Formato deseado:
					if (pHexReturn)
					{
						ret = hexCode;
					}
					else
					{
						//Quieren la Respuesta en formato Numerico (Entero)
						if (pInvierte)
						{
							//Convierte Hex a Int32:
							Int32 _UID = Int32.Parse(hexCode, System.Globalization.NumberStyles.HexNumber);
							ret = UID.ToString();
						}
						else
						{
							//Convierte Hex a Int64:
							Int64 _UID = Int64.Parse(hexCode, System.Globalization.NumberStyles.HexNumber);
							ret = _UID.ToString();
						}
					}
				}
				else
				{
					ret = string.Format("{0}\r\n{1}", ret_Bytes.Return.descripcion, ret_Bytes.Status.descripcion);
				}
			}
			catch (ArgumentOutOfRangeException ex)
			{
				throw ex;
			}
			catch (Exception ex)
			{
				throw ex;
			}
			return ret;
		}




		private string[] getListaLlavesTransporte()
		{
			string[] arr = { "FFFFFFFFFFFF", "A0A1A2A3A4A5", "B0B1B2B3B4B5" };
			return arr;
		}

		/// <summary>Metodo que Autentica la Tarjeta para poder Leer o Escribir Datos en ella.</summary>
		/// <param name="key">Clave de Transporte.</param>
		public void AutenticarTarjeta(string key, int pBloque)
		{
			//Obtiene el tamaño de la Clave:
			int keyLen = (int)Math.Round((double)(((double)key.ToString().Length) / 2));

			string text2 = "".PadRight(256, ' '); //<- Crea un Buffer para recibir los datos.

			//Comando que Carga la Clave en la Tarjeta: (STORAGE_CARD_CMDS_LOAD_KEYS)
			byte[] commando = createAPDU_Command("FF", "82", "00", "60", "06", "FFFFFFFFFFFF");
			byte[] retorno = new byte[255];

			//CardTransmit(commando, ref retorno);
			//string text3 = text2.Substring(0, 5);
			//if (text3 != "90 00") //<-Si Hay Error
			//{
			//	throw new Exception(errorStatusWord(text3));
			//}
			//else
			//{
			//Comando que Realiza la Autenticacion: (STORAGE_CARD_CMDS_AUTHENTICATE)
			commando = createAPDU_Command("FF", "86", "00", "00", "05", string.Format("0100{0:00}6000", pBloque));
			retorno = new byte[255];
			//CardTransmit(commando, ref retorno);

			//}
		}

		/// <summary>Obtine informacion basica de la Tarjeta.</summary>
		public string GetCardInfo()
		{
			string checkError = string.Empty;
			//string retorno = "".PadLeft(256, ' ');
			//string commando = "FF CC 00 00 01 11";

			//CardTransmit(commando, ref retorno);
			APDU_Response ret_Bytes = CardTransmit(createAPDU_Command(0xFF, 0xCC, 0x00, 0x00, 0x01, 0x11));
			if (ret_Bytes != null && ret_Bytes.Status.hex_value == "90 00")
			{
				string salida = ret_Bytes.Data_HexString;// retorno.Substring(0, 8);
				string[] bytes = salida.Split(new char[] { ' ' });
				StringBuilder info = new StringBuilder();

				if (bytes.Length == 3)
				{
					switch (bytes[0])
					{
						case "00": info.AppendLine("No Card Present"); break;
						case "01": info.AppendLine("Card Present, "); break;
					}
					switch (bytes[1])
					{
						case "00": info.AppendLine("Baud Rate 106Kbps in both directions "); break;
						case "01": info.AppendLine("Baud Rate 106Kbps from PICC to PCD, 212Kbps from PCD to PICC "); break;
						case "02": info.AppendLine("Baud Rate 106Kbps from PICC to PCD, 424Kbps from PCD to PICC "); break;
						case "03": info.AppendLine("Baud Rate 106Kbps from PICC to PCD, 848Kbps from PCD to PICC "); break;
						case "10": info.AppendLine("Baud Rate 212Kbps from PICC to PCD, 106Kbps from PCD to PICC "); break;
						case "11": info.AppendLine("Baud Rate 212Kbps in both directions "); break;
						case "12": info.AppendLine("Baud Rate 212Kbps from PICC to PCD, 424Kbps from PCD to PICC "); break;
						case "": info.AppendLine("Baud Rate Unknown"); break;
					}
					switch (bytes[2])
					{
						case "00": info.AppendLine("Memory Card Type A"); break;
						case "01": info.AppendLine("Memory Card Type B"); break;
						case "10": info.AppendLine("T=CL Card Type A"); break;
						case "11": info.AppendLine("T=CL Card Type B"); break;
						case "20": info.AppendLine("Dual Mode Card Type A"); break;
						case "21": info.AppendLine("Dual Mode Card Type B"); break;
					}
					info.AppendLine(string.Format("UID={0} ATR?: {1}", GetCardUID(true), 0));
				}

				return info.ToString();
			}
			else
			{
				throw new Exception(ret_Bytes.Return.descripcion);
			}
		}


		/// <summary>Case 1 Command: No incluye Data, NO Requiere Respuesta, Response: Trailer</summary>
		/// <param name="CLA">Class byte: ejem: 0x00 o 0xFF</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		public byte[] createAPDU_Command(byte CLA, byte INS, byte P1, byte P2)
		{
			byte[] _ret = new byte[] { CLA, INS, P1, P2 };
			return _ret;
		}

		/// <summary>Case 1 Command: No incluye Data, NO Requiere Respuesta, Response: Trailer</summary>
		/// <param name="CLA">Class byte: ejem: 0x00 o 0xFF</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		public byte[] createAPDU_Command(string CLA, string INS, string P1, string P2)
		{
			byte[] _ret = new byte[4];
			try
			{
				_ret[0] = byte.Parse(CLA, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
				_ret[1] = byte.Parse(INS, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
				_ret[2] = byte.Parse(P1, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
				_ret[3] = byte.Parse(P2, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			}
			catch { }
			return _ret;
		}


		/// <summary>Case 2 APDU Command: No incluye Data, Requiere Respuesta, Response: Data + Trailer</summary>
		/// <param name="CLA">Class byte: 0x00</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="Le">Number of data bytes expected in the response. If Le is 0x00, at maximum 256 bytes are expected.</param>
		public byte[] createAPDU_Command(string CLA, string INS, string P1, string P2, string Le)
		{
			byte[] _ret = new byte[5]; //_ret = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
			try
			{
				_ret[0] = byte.Parse(CLA, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
				_ret[1] = byte.Parse(INS, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
				_ret[2] = byte.Parse(P1, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
				_ret[3] = byte.Parse(P2, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
				_ret[4] = byte.Parse(Le, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			}
			catch { }
			return _ret;
		}

		/// <summary>Case 2 Command: No incluye Data, Requiere Respuesta, Response: Data + Trailer</summary>
		/// <param name="CLA">Class byte: ejem: 0x00 o 0xFF</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="Le">Number of data bytes expected in the response. If Le is 0x00, at maximum 256 bytes are expected.</param>
		public byte[] createAPDU_Command(byte CLA, byte INS, byte P1, byte P2, byte Le)
		{
			byte[] _ret = new byte[] { CLA, INS, P1, P2, Le };
			return _ret;
		}


		/// <summary>Case 3 Command: Incluye Data, NO Requiere Respuesta, Response: Trailer</summary>
		/// <param name="CLA">Class byte: 0x00</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="Lc">Number of data bytes (lenght) (in Hex) send to the card.</param>
		/// <param name="Data">Data bytes: Ej: 'FFCA00FFCA' </param>
		public byte[] createAPDU_Command(string CLA, string INS, string P1, string P2, string Lc, string Data)
		{
			byte[] APDU = new byte[5];
			APDU[0] = byte.Parse(CLA, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[1] = byte.Parse(INS, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[2] = byte.Parse(P1, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[3] = byte.Parse(P2, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[4] = byte.Parse(Lc, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);

			byte[] _Data = hexaToByteArray(Data);
			byte[] _ret = new byte[APDU.Length + _Data.Length];

			Buffer.BlockCopy(APDU, 0, _ret, 0, APDU.Length);
			Buffer.BlockCopy(_Data, 0, _ret, APDU.Length, _Data.Length);

			return _ret;
		}

		/// <summary>Case 3 Command: Incluye Data, NO Requiere Respuesta, Response: Trailer</summary>
		/// <param name="CLA">Class byte: 0x00</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="Lc">Number of data bytes (lenght) (in Hex) send to the card.</param>
		/// <param name="Data">Data bytes: Ej: 0xFF, 0xCA, 0x00, 0x00, 0x00 </param>
		public byte[] createAPDU_Command(byte CLA, byte INS, byte P1, byte P2, byte Lc, params byte[] Data)
		{
			byte[] APDU = new byte[] { CLA, INS, P1, P2, Lc };
			byte[] _ret = new byte[APDU.Length + Data.Length];

			Buffer.BlockCopy(APDU, 0, _ret, 0, APDU.Length);
			Buffer.BlockCopy(Data, 0, _ret, APDU.Length, Data.Length);
			return _ret;
		}


		/// <summary>>Case 4 Command: Incluye Data, Requiere Respuesta, Response: Data + Trailer.</summary>
		/// <param name="CLA">Class byte: 0x00</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="Lc">Number of data bytes send to the card.</param>
		/// <param name="Data">Data bytes: Ej: 'A0 00 00 18 40 00 00 01 63'</param>
		/// <param name="Le">Number of data bytes (lenght) expected in the response. If Le is 0x00, at maximum 256 bytes are expected.</param>
		public byte[] createAPDU_Command(string CLA, string INS, string P1, string P2, string Lc, string Data, string Le)
		{
			byte[] APDU = new byte[5];
			APDU[0] = byte.Parse(CLA, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[1] = byte.Parse(INS, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[2] = byte.Parse(P1, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[3] = byte.Parse(P2, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
			APDU[4] = byte.Parse(Lc, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);

			byte[] _Data = hexaToByteArray(Data);
			byte[] _ret = new byte[APDU.Length + _Data.Length + 1];

			Buffer.BlockCopy(APDU, 0, _ret, 0, APDU.Length);
			Buffer.BlockCopy(_Data, 0, _ret, APDU.Length, _Data.Length);

			_ret[_ret.Length] = byte.Parse(Le, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);

			return _ret;
		}

		/// <summary>Case 4 Command: Incluye Data, Requiere Respuesta, Response: Data + Trailer.</summary>
		/// <param name="CLA">Class byte: 0x00</param>
		/// <param name="INS">Instruction byte: 0xA4:Select Command, 0xB2:Read Record Command</param>
		/// <param name="P1">Parameter 1 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="P2">Parameter 2 byte: The value and meaning depends on the instruction code(INS).</param>
		/// <param name="Lc">Number of data bytes send to the card.</param>
		/// <param name="Data">Data bytes: Ej: 'new byte[] { 0xA0, 0x00, 0xFF, 0x00, 0xCA }'</param>
		/// <param name="Le">Number of data bytes (lenght) expected in the response. If Le is 0x00, at maximum 256 bytes are expected.</param>
		public byte[] createAPDU_Command(byte CLA, byte INS, byte P1, byte P2, byte Lc, byte[] Data, byte Le)
		{
			byte[] APDU = new byte[] { CLA, INS, P1, P2, Lc };
			byte[] _ret = new byte[APDU.Length + Data.Length];

			Buffer.BlockCopy(APDU, 0, _ret, 0, APDU.Length);
			Buffer.BlockCopy(Data, 0, _ret, APDU.Length, Data.Length);

			APDU = new byte[_ret.Length + 1];
			Buffer.BlockCopy(_ret, 0, APDU, 0, _ret.Length);

			APDU[APDU.Length - 1] = Le;
			return APDU;
		}


		public string APDU_ToString(byte[] APDUcommand)
		{
			string _ret = string.Empty;
			if (APDUcommand != null && APDUcommand.Length > 0)
			{
				int Len = APDUcommand.Length;

				if (APDUcommand.Length == 4) //<- Case 1
				{	
					_ret = string.Format("{{ 'APDU':{{ 'CLA':'{0}', 'INS':'{1}', 'P1':'{2}', 'P2':'{3}' }} }}",
							System.Convert.ToString(APDUcommand[0], 16).PadLeft(2, '0').ToUpper(),
							System.Convert.ToString(APDUcommand[1], 16).PadLeft(2, '0').ToUpper(),
							System.Convert.ToString(APDUcommand[2], 16).PadLeft(2, '0').ToUpper(),
							System.Convert.ToString(APDUcommand[3], 16).PadLeft(2, '0').ToUpper()
						);
				}
				else if (APDUcommand.Length == 5) //<- Case 2
				{
					_ret = string.Format("{{ 'APDU':{{ 'CLA':'{0}', 'INS':'{1}', 'P1':'{2}', 'P2':'{3}', 'Le':'{4}' }} }}",
							System.Convert.ToString(APDUcommand[0], 16).PadLeft(2, '0').ToUpper(),
							System.Convert.ToString(APDUcommand[1], 16).PadLeft(2, '0').ToUpper(),
							System.Convert.ToString(APDUcommand[2], 16).PadLeft(2, '0').ToUpper(),
							System.Convert.ToString(APDUcommand[3], 16).PadLeft(2, '0').ToUpper(),
							System.Convert.ToString(APDUcommand[4], 16).PadLeft(2, '0').ToUpper()
						);
				}
				else if(APDUcommand.Length >= 6) //<- Case 3 y 4
				{					
					_ret = string.Format("{{ 'APDU':{{ 'CLA':'{0}', 'INS':'{1}', 'P1':'{2}', 'P2':'{3}', 'Lc':'{4}'",
						System.Convert.ToString(APDUcommand[0], 16).PadLeft(2, '0').ToUpper(),
						System.Convert.ToString(APDUcommand[1], 16).PadLeft(2, '0').ToUpper(),
						System.Convert.ToString(APDUcommand[2], 16).PadLeft(2, '0').ToUpper(),
						System.Convert.ToString(APDUcommand[3], 16).PadLeft(2, '0').ToUpper(),
						System.Convert.ToString(APDUcommand[4], 16).PadLeft(2, '0').ToUpper()
					);

					if (APDUcommand.Length >= 6)
					{
						int dataLen = APDUcommand[4];
						byte[] Data = new byte[dataLen]; 
						Array.Copy(APDUcommand, 5, Data, 0, dataLen);

						if (Data.Length > 0)
						{
							_ret += string.Format(", 'Data':'{0}'", byteToHexa(Data, dataLen, true) );
						}

						if (APDUcommand.Length > (5 + dataLen))
						{
							int index = 5 + dataLen;
							_ret += string.Format(", 'Le':'{0}'", System.Convert.ToString(APDUcommand[index], 16).PadLeft(2, '0').ToUpper());
						}						
					}
					_ret += " } }";
				}				
			}
			return _ret;
		}

		/// <summary>Convierte una cadena Hexadecimal en un Array de Bytes.</summary>
		/// <param name="commando"></param>
		internal static byte[] hexaToByteArray(string commando)
		{
			int largoCommando = commando.Length;
			if (((largoCommando % 2) != 0) | (largoCommando == 0))
			{
				throw new Exception("Commando no Valido");
			}

			byte[] resultado = new byte[largoCommando / 2];

			string stringByte = string.Empty;
			byte miByte = 0;
			byte contador = 0;
			for (int i = 0; i <= (largoCommando - 2); i += 2)
			{
				stringByte = commando.Substring(i, 2);
				miByte = System.Convert.ToByte(stringByte, 16);

				if (miByte > 255)
				{
					throw new Exception("Caracter invalido al procesar comando");
				}
				resultado[contador] = miByte;
				contador++;
			}

			return resultado;
		}

		/// <summary>Convierte un Array de Bytes en una cadena Hexadecimal.</summary>
		/// <param name="byReadBuffer">Array de Bytes a Convertir</param>
		/// <param name="lenght">Longitud de la cadena</param>
		/// <param name="bSpace">'true'=Agrega Espacios entre los pares de bytes.</param>
		public string byteToHexa(byte[] byReadBuffer, int lenght, bool bSpace)
		{
			string text2 = "";
			for (short num1 = 0; num1 < lenght; num1 = (short)(num1 + 1))
			{
				short num2 = byReadBuffer[num1];

				text2 = text2 + System.Convert.ToString(num2, 16).PadLeft(2, '0').ToUpper();

				if (bSpace)
				{
					text2 = text2 + " ";
				}
			}
			return text2.ToUpper();
		}


		#endregion

		#region Metodos Privados

		private string HexToString(string pHexCode)
		{
			string retorno = string.Empty;
			StringBuilder cadena = new StringBuilder();
			try
			{
				//retorno = ToHex(pHexCode);
				string[] X = pHexCode.Split(new char[] { ' ' });
				foreach (string code in X)
				{
					cadena.Append(Convert.ToString(Convert.ToChar(Int32.Parse(code, System.Globalization.NumberStyles.HexNumber))));
				}
				retorno = cadena.ToString();
			}
			catch (Exception ex)
			{
				throw ex;
			}
			return retorno;
		}

		private string ConvertStringToHex(string asciiString)
		{
			string hex = "";
			foreach (char c in asciiString)
			{
				int tmp = c;
				hex += String.Format("{0:x2}", (uint)System.Convert.ToUInt32(tmp.ToString()));
			}
			return hex;
		}

		private string ConvertHexToString(string HexValue)
		{
			string StrValue = "";
			while (HexValue.Length > 0)
			{
				StrValue += System.Convert.ToChar(System.Convert.ToUInt32(HexValue.Substring(0, 2), 16)).ToString();
				HexValue = HexValue.Substring(2, HexValue.Length - 2);
			}
			return StrValue;
		}

		#endregion

		#endregion

		#region IDisposable Support

		//IDisposable
		private void Dispose(bool disposing)
		{
			if (!this._disposed)
			{
				if (disposing)
				{
					// Free other state (managed objects).            
				}

				//Free your own state (unmanaged objects).
				//Set large fields to null.
				this._states = null;
				this._Bworker.CancelAsync();
				this._Bworker.Dispose();

				if (this._CardContext != IntPtr.Zero) UnsafeNativeMethods.SCardDisconnect(this._CardContext, 0);
				if (this._ReaderContext != IntPtr.Zero) UnsafeNativeMethods.SCardReleaseContext(this._ReaderContext);
			}
			this._disposed = true;
		}

		// Implement IDisposable.
		// Do not make this method virtual.
		// A derived class should not be able to override this method.
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		#endregion

		#region Properties

		private bool HasContext
		{
			get { return (this._ReaderContext != IntPtr.Zero); }
		}

		/// <summary>Obtiene la Lista de Lectores Conectados al Sistema.</summary>
		public List<string> LectoresDisponibles
		{
			get { return this.mAvailableReaders; }
		}

		/// <summary>Obtiene o establece el ATR de la tarjeta conectada.</summary>
		public List<string> ATR { get; set; }

		#endregion

	}

	public class APDU_Response
	{ 
		public APDU_Response() { }
		public APDU_Response(byte[] full_response, int ret_code, int len_response, bool trim_response = true)
		{
			if (full_response != null && full_response.Length > 0)
			{
				this.Response = new byte[len_response]; //Ajustar el Tamaño del Array Devuelto
				if (len_response > 255) len_response = 255;
				Array.Copy(full_response, 0, this.Response, 0, len_response);

				switch (Response.Length)
				{
					case 1:
						this.sw1 = this.Response[this.Response.Length - 1];
						break;
					default:
						this.sw1 = this.Response[this.Response.Length - 2];
						this.sw2 = this.Response[this.Response.Length - 1];
						break;
				}

				//Ver si tenemos Datos para la Respuesta:
				if (this.Response.Length - 2 > 0)
				{
					if (trim_response)
					{
						this.Data = new byte[this.Response.Length - 2]; //Extraer el Data de la Respuesta
						Array.Copy(this.Response, 0, this.Data, 0, this.Response.Length - 2);
					}
					else
					{
						//Esto es para los casos en que queremos la respuesta competa, ejem: para el ATR
						this.Data = this.Response;
					}
				}
			}
			else if (full_response != null && full_response.Length == 1)
			{
				this.sw1 = full_response[full_response.Length - 1];
			}
			this.Return = new APDU_Return(ret_code);
			this.Status = new APDU_Status(this.sw1, this.sw2);
		}

		/// <summary>Bytes completos de la Respuesta.</summary>
		public byte[] Response { get; set; }

		/// <summary>Bytes completos de la Respuesta.</summary>
		public string Response_HexString
		{
			get
			{
				string _ret = string.Empty;
				if (this.Response != null && this.Response.Length > 0)
				{
					_ret = ByteArrayToHex(this.Response, this.Response.Length);
				}
				return _ret;
			}
		}

		/// <summary>[Optional] Datos de la Respuesta, hasta 255 bytes.</summary>
		public byte[] Data { get; set; }

		/// <summary>[Optional] Datos de la Respuesta, en formato Hexadecimal.</summary>
		public string Data_HexString
		{
			get
			{
				string _ret = "00";
				if (this.has_data)
				{
					_ret = ByteArrayToHex(this.Data, this.Data.Length);
				}
				else
				{
					this.Data = new byte[0];
				}
				return _ret;
			}
		}

		/// <summary>[Obligatorio] Status Bytes used by the card to indicate the result of the processing to the terminal.
		/// If the status bytes are '90 00' then the process has completed.</summary>
		public byte sw1 { get; set; }

		/// <summary>[Optional] Status Bytes used by the card to indicate the result of the processing to the terminal.
		/// If the status bytes are '90 00' then the process has completed.</summary>
		public byte sw2 { get; set; }

		/// <summary>Codigo de Retorno del Comando.</summary>
		public APDU_Return Return { get; set; }

		/// <summary>Estado de la Respuesta Obtenida.</summary>
		public APDU_Status Status { get; set; }

		/// <summary>Determina si hay error en la Respuesta.</summary>
		public bool is_ok
		{
			get
			{
				bool _ret = true;
				if (this.Return != null && this.Return.code != 0) _ret = false;
				if (this.Status != null)
				{
					if (this.Status.hex_value != "0000" && this.Status.hex_value != "9000")
					{
						_ret = false;
					}
				}
				return _ret;
			}
			
		}

		/// <summary>Determina si hay Datos en la Respuesta.
		/// <para>Reviso los 4 primeros bytes para ver si contienen algun dato</para></summary>
		public bool has_data
		{
			get
			{
				bool _ret = false;
				if (this.Data != null && this.Data.Length > 0)
				{
					if (this.Data.Length >= 4)
					{
						foreach (byte _Byte in this.Data)
						{
							if (_Byte != 0x00) _ret = true; break;
						}
					}
				}
				return _ret;
			}
		}

		public override string ToString()
		{
			return String.Format("Response:{{ Data:'{0}', {1}, {2} }}", this.Data_HexString, Status.ToString(), Return.ToString() );
		}

		/// <summary>Convierte un Array de Bytes en una cadena Hexadecimal.</summary>
		/// <param name="bytes">Array de Bytes a Convertir</param>
		/// <param name="lenght">Longitud de la cadena</param>
		/// <param name="bSpace">'true'= Agrega Espacios entre los pares de bytes.</param>
		private string ByteArrayToHex(byte[] bytes, int lenght, bool bSpace = true)
		{
			string text2 = "";
			for (short num1 = 0; num1 < lenght; num1 = (short)(num1 + 1))
			{
				short num2 = bytes[num1];

				text2 = text2 + System.Convert.ToString(num2, 16).PadLeft(2, '0');

				if (bSpace)
				{
					text2 = text2 + " ";
				}
			}
			return text2.ToUpper();
		}
	}

	public class APDU_Status
	{
		private byte[] _status = new byte[2];

		public APDU_Status(byte sw1, byte sw2)
		{
			this.sw1 = sw1;
			this.sw2 = sw2;

			this._status[0] = this.sw1;
			this._status[1] = this.sw2;
		}

		/// <summary>[Obligatorio] Status Bytes used by the card to indicate the result of the processing to the terminal.</summary>
		public byte sw1 { get; set; }

		/// <summary>[Optional] Status Bytes used by the card to indicate the result of the processing to the terminal.</summary>
		public byte sw2 { get; set; }

		/// <summary>Codigo del Estado en formato Hexadecimal.</summary>
		public string hex_value
		{
			get { return ByteArrayToHex(this._status, 2, false); }
		}

		/// <summary>Descripcion del Estado</summary>
		public string descripcion
		{
			get
			{
				string _ret = "Unknown";

				if (sw1 == 0x00) _ret = "Command Executed.";

				if (sw1 == 0x06) _ret = "ERROR|Class not supported.";

				if (sw1 == 0x61)
				{
					_ret = string.Format("INFO|Command successfully executed; {0} bytes of data are available and can be requested using GET RESPONSE.", sw2);
				}

				if (sw1 == 0x62)
				{
					if (sw2 == 0x00) _ret = "WARNING|No information given(NV-Ram not changed).";
					if (sw2 == 0x01) _ret = "WARNING|NV-Ram not changed 1.";
					if (sw2 == 0x81) _ret = "WARNING|Part of returned data may be corrupted.";
					if (sw2 == 0x82) _ret = "WARNING|End of data reached before Le bytes (Le is greater than data length)";
					if (sw2 == 0x83) _ret = "WARNING|Selected file invalidated.";
					if (sw2 == 0x84) _ret = "WARNING|Selected file is not valid. FCI not formated according to ISO.";
					if (sw2 == 0x85) _ret = "WARNING|No input data available from a sensor on the card.No Purse Engine enslaved for R3bc.";
					if (sw2 == 0xA2) _ret = "WARNING|Wrong R-MAC";
					if (sw2 == 0xA4) _ret = "WARNING|Card locked (during reset)";
					if (sw2 == 0xF1) _ret = "WARNING|Wrong C-MAC";
					if (sw2 == 0xF3) _ret = "WARNING|Internal reset";
					if (sw2 == 0xF5) _ret = "WARNING|Default agent locked";
					if (sw2 == 0xF7) _ret = "WARNING|Cardholder locked";
					if (sw2 == 0xF8) _ret = "WARNING|Basement is current agent";
					if (sw2 == 0xF9) _ret = "WARNING|CALC Key Set not unblocked";
					else _ret = "WARNING|RFU. State of non-volatile memory unchanged";
				}

				if (sw1 == 0x63)
				{
					if (sw2 == 0x00) _ret = "WARNING|No information given(NV-Ram changed)";
					if (sw2 == 0x81) _ret = "WARNING|File filled up by the last write.Loading/updating is not allowed.";
					if (sw2 == 0x82) _ret = "WARNING|Card key not supported.";
					if (sw2 == 0x83) _ret = "WARNING|Reader key not supported.";
					if (sw2 == 0x84) _ret = "WARNING|Plaintext transmission not supported.";
					if (sw2 == 0x85) _ret = "WARNING|Secured transmission not supported.";
					if (sw2 == 0x86) _ret = "WARNING|Volatile memory is not available.";
					if (sw2 == 0x87) _ret = "WARNING|Non-volatile memory is not available.";
					if (sw2 == 0x88) _ret = "WARNING|Key number not valid.";
					if (sw2 == 0x89) _ret = "WARNING|Key length is not correct.";
					if (sw2 == 0xC0) _ret = "WARNING|Verify fail, no try left.";
					if (sw2 == 0xC1) _ret = "WARNING|Verify fail, 1 try left.";
					if (sw2 == 0xC2) _ret = "WARNING|Verify fail, 2 tries left.";
					if (sw2 == 0xC3) _ret = "WARNING|Verify fail, 3 tries left.";
					if (sw2 == 0xF1) _ret = "WARNING|More data expected.";
					if (sw2 == 0xF2) _ret = "WARNING|More data expected and proactive command pending.";
					else _ret = "WARNING|RFU. State of non-volatile memory unchanged";
				}

				if (sw1 == 0x64)
				{
					if (sw2 == 0x00) _ret = "ERROR|No information given(NV-Ram not changed)";
					else if (sw2 == 0x01) _ret = "ERROR|Command timeout. Immediate response required by the card.";
					else _ret = "ERROR|RFU. State of non-volatile memory unchanged";
				}

				if (sw1 == 0x65)
				{
					if (sw2 == 0x00) _ret = "ERROR|No information given";
					else if (sw2 == 0x01) _ret = "ERROR|Write error.Memory failure. There have been problems in writing or reading the EEPROM.Other hardware problems may also bring this error.";
					else if (sw2 == 0x81) _ret = "ERROR|Memory failure";
					else _ret = "ERROR|RFU. State of non-volatile memory unchanged";
				}

				if (sw1 == 0x66)
				{
					if (sw2 == 0x00) _ret = "SECURITY|Error while receiving (timeout)";
					else if (sw2 == 0x01) _ret = "SECURITY|Error while receiving (character parity error)";
					else if (sw2 == 0x01) _ret = "SECURITY|Wrong checksum";
					else if (sw2 == 0x01) _ret = "SECURITY|The current DF file without FCI";
					else if (sw2 == 0x01) _ret = "SECURITY|No SF or KF under the current DF";
					else if (sw2 == 0x01) _ret = "SECURITY|Incorrect Encryption/Decryption Padding";
					else _ret = "SECURITY|Security Error";
				}

				if (sw1 == 0x67)
				{
					if (sw2 == 0x00) _ret = "ERROR|Wrong length";
					else _ret = "ERROR|length incorrect(procedure)(ISO 7816-3)";
				}

				if (sw1 == 0x68)
				{
					if (sw2 == 0x00) _ret = "ERROR|No information given(The request function is not supported by the card)";
					else if (sw2 == 0x81) _ret = "ERROR|Logical channel not supported";
					else if (sw2 == 0x82) _ret = "ERROR|Secure messaging not supported";
					else if (sw2 == 0x83) _ret = "ERROR|Last command of the chain expected";
					else if (sw2 == 0x84) _ret = "ERROR|Command chaining not supported";
					else _ret = "ERROR|Functions in CLA not supported";
				}

				if (sw1 == 0x69)
				{
					if (sw2 == 0x00) _ret = "ERROR|No information given(Command not allowed)";
					else if (sw2 == 0x01) _ret = "ERROR|Command not accepted(inactive state)";
					else if (sw2 == 0x81) _ret = "ERROR|Command incompatible with file structure";
					else if (sw2 == 0x82) _ret = "ERROR|Security condition not satisfied.";
					else if (sw2 == 0x83) _ret = "ERROR|Authentication method blocked";
					else if (sw2 == 0x84) _ret = "ERROR|Referenced data reversibly blocked(invalidated)";
					else if (sw2 == 0x85) _ret = "ERROR|Conditions of use not satisfied.";
					else if (sw2 == 0x86) _ret = "ERROR|Command not allowed (no current EF)";
					else if (sw2 == 0x87) _ret = "ERROR|Expected secure messaging(SM) object missing";
					else if (sw2 == 0x88) _ret = "ERROR|Incorrect secure messaging(SM) data object";
					else if (sw2 == 0x8D) _ret = "ERROR|Reserved";

					else if (sw2 == 0x96) _ret = "ERROR|Data must be updated again";
					else if (sw2 == 0xE1) _ret = "ERROR|POL1 of the currently Enabled Profile prevents this action.";
					else if (sw2 == 0xF0) _ret = "ERROR|Permission Denied";
					else if (sw2 == 0xF1) _ret = "ERROR|Permission Denied – Missing Privilege";
					else _ret = "ERROR|Command not allowed";
				}

				if (sw1 == 0x6A)
				{
					if (sw1 == 0x6A && sw2 == 0x00) _ret = "ERROR|No information given (Bytes P1 and/or P2 are incorrect)";
					else if (sw2 == 0x80) _ret = "ERROR|The parameters in the data field are incorrect.";
					else if (sw2 == 0x81) _ret = "ERROR|Function not supported";
					else if (sw2 == 0x82) _ret = "ERROR|File not found (no such block or no such offset in the card)";
					else if (sw2 == 0x83) _ret = "ERROR|Record not found";
					else if (sw2 == 0x84) _ret = "ERROR|There is insufficient memory space in record or file";
					else if (sw2 == 0x85) _ret = "ERROR|Lc inconsistent with TLV structure";
					else if (sw2 == 0x86) _ret = "ERROR|Incorrect P1 or P2 parameter.";
					else if (sw2 == 0x87) _ret = "ERROR|Lc inconsistent with P1-P2";
					else if (sw2 == 0x88) _ret = "ERROR|Referenced data not found";
					else if (sw2 == 0x89) _ret = "ERROR|File already exists";
					else if (sw2 == 0x8A) _ret = "ERROR|DF name already exists.";
					else if (sw2 == 0xF0) _ret = "ERROR|Wrong parameter value.";
					else _ret = "ERROR|Wrong parameter(s) P1-P2";
				}

				if (sw1 == 0x6B)
				{
					if (sw2 == 0x00) _ret = "ERROR|Wrong parameter(s) P1-P2";
					else _ret = "ERROR|Reference incorrect (procedure byte), (ISO 7816-3)";
				}

				if (sw1 == 0x6C)
				{
					if (sw2 == 0x00) _ret = "ERROR|Incorrect P3 length.";
					else _ret = string.Format("ERROR|Bad length value in Le; '{0}' is the correct Le",
						System.Convert.ToString(sw2, 16).PadLeft(2, '0').ToUpper() );
				}

				if (sw1 == 0x6D)
				{
					if (sw2 == 0x00) _ret = "ERROR|Instruction code not supported or invalid.";
					else _ret = "ERROR|Instruction code not programmed or invalid (procedure byte), (ISO 7816-3)";
				}

				if (sw1 == 0x6E)
				{
					if (sw2 == 0x00) _ret = "ERROR|Instruction Class (CLS) not supported.";
					else _ret = "ERROR|Instruction class not supported(procedure byte), (ISO 7816-3).";
				}

				if (sw1 == 0x6F)
				{
					if (sw2 == 0x00) _ret = "ERROR|Command aborted – more exact diagnosis not possible(e.g., operating system error).";
					else if (sw2 == 0xFF) _ret = "ERROR|Card dead(overuse, etc.)";
					else _ret = "ERROR|Internal exception. No precise diagnosis(procedure byte), (ISO 7816-3)";
				}

				if (sw1 == 0x90)
				{
					if (sw2 == 0x00) _ret = "INFO|Command successfully executed(OK).";
					else if (sw2 == 0x04) _ret = "WARNING|PIN not succesfully verified, 3 or more PIN tries left";
					else if (sw2 == 0x08) _ret = "ERROR|Key/file not found";
					else if (sw2 == 0x80) _ret = "WARNING|Unblock Try Counter has reached zero";
					else _ret = "ERROR|-";
				}

				if (sw1 == 0x91)
				{
					if (sw2 == 0x00) _ret = "INFO|OK.";
					else if (sw2 == 0x01) _ret = "WARNING|States.activity, States.lock Status or States.lockable has wrong value";
					else if (sw2 == 0x02) _ret = "INFO|Transaction number reached its limit";
					else if (sw2 == 0x0C) _ret = "INFO|No changes";
					else if (sw2 == 0x0E) _ret = "ERROR|Insufficient NV-Memory to complete command";
					else if (sw2 == 0x1C) _ret = "ERROR|Command code not supported";
					else if (sw2 == 0x1E) _ret = "ERROR|CRC or MAC does not match data ";
					else if (sw2 == 0x40) _ret = "ERROR|Invalid key number specified";
					else if (sw2 == 0x7E) _ret = "ERROR|Length of command string invalid";
					else if (sw2 == 0x9D) _ret = "ERROR|Not allow the requested command";
					else if (sw2 == 0x9E) _ret = "ERROR|Value of the parameter invalid";
					else if (sw2 == 0xA0) _ret = "ERROR|Requested AID not present on PICC";
					else if (sw2 == 0xA1) _ret = "ERROR|Unrecoverable error within application";
					else if (sw2 == 0xAE) _ret = "ERROR|Authentication status does not allow the requested command";
					else if (sw2 == 0xAF) _ret = "ERROR|Additional data frame is expected to be sent";
					else if (sw2 == 0xBE) _ret = "ERROR|Out of boundary";
					else if (sw2 == 0xC1) _ret = "ERROR|Unrecoverable error within PICC";
					else if (sw2 == 0xCA) _ret = "ERROR|Previous Command was not fully completed";
					else if (sw2 == 0xCD) _ret = "ERROR|PICC was disabled by an unrecoverable error";
					else if (sw2 == 0xCE) _ret = "ERROR|Number of Applications limited to 28";
					else if (sw2 == 0xDE) _ret = "ERROR|File or application already exists";
					else if (sw2 == 0xEE) _ret = "ERROR|Could not complete NV-write operation due to loss of power";
					else if (sw2 == 0xF0) _ret = "ERROR|Specified file number does not exist";
					else if (sw2 == 0xF1) _ret = "ERROR|Unrecoverable error within file";
					else _ret = "ERROR|Unknown";
				}

				if (sw1 == 0x92)
				{
					if (sw2 == 0x10) _ret = "ERROR|Insufficient memory.No more storage available.";
					else if (sw2 == 0x40) _ret = "ERROR|Writing to EEPROM not successful.";
					else _ret = string.Format("INFORMATION|Writing to EEPROM successful after '{0}' attempts.", sw2);
				}

				if (sw1 == 0x93)
				{
					if (sw2 == 0x01) _ret = "ERROR|Integrity error";
					else if (sw2 == 0x02) _ret = "ERROR|Candidate S2 invalid";
					else if (sw2 == 0x03) _ret = "ERROR|Application is permanently locked";
					else _ret = "ERROR|Unknown";
				}

				if (sw1 == 0x94)
				{
					if (sw2 == 0x00) _ret = "ERROR|No EF selected.";
					else if (sw2 == 0x01) _ret = "ERROR|Candidate currency code does not match purse currency";
					else if (sw2 == 0x02) _ret = "ERROR|Candidate amount too high, Address range exceeded.";
					else if (sw2 == 0x03) _ret = "ERROR|Candidate amount too low";
					else if (sw2 == 0x04) _ret = "ERROR|FID not found, record not found or comparison pattern not found.";
					else if (sw2 == 0x05) _ret = "ERROR|Problems in the data field";
					else if (sw2 == 0x06) _ret = "ERROR|Required MAC unavailable";
					else if (sw2 == 0x07) _ret = "ERROR|Bad currency : purse engine has no slot with R3bc currency";
					else if (sw2 == 0x08) _ret = "ERROR|Selected file type does not match command.";
					else _ret = "ERROR|Unknown";
				}

				if (sw1 == 0x95)
				{
					if (sw2 == 0x80) _ret = "ERROR|Bad sequence";
					else _ret = "ERROR|Unknown";
				}

				if (sw1 == 0x96)
				{
					if (sw2 == 0x81) _ret = "ERROR|Slave not found";
					else _ret = "ERROR|Unknown";
				}

				if (sw1 == 0x97)
				{
					if (sw2 == 0x00) _ret = "ERROR|PIN blocked and Unblock Try Counter is 1 or 2";
					else if (sw2 == 0x01) _ret = "ERROR|";
					else if (sw2 == 0x02) _ret = "ERROR|Main keys are blocked";
					else if (sw2 == 0x04) _ret = "ERROR|PIN not succesfully verified, 3 or more PIN tries left";
					else if (sw2 == 0x84) _ret = "ERROR|Base key";
					else if (sw2 == 0x85) _ret = "ERROR|Limit exceeded – C-MAC key";
					else if (sw2 == 0x86) _ret = "ERROR|SM error – Limit exceeded – R-MAC key";
					else if (sw2 == 0x87) _ret = "ERROR|Limit exceeded – sequence counter";
					else if (sw2 == 0x88) _ret = "ERROR|Limit exceeded – R-MAC length";
					else if (sw2 == 0x89) _ret = "ERROR|Service not available";
					else _ret = "ERROR|PIN Error Unknown";
				}

				if (sw1 == 0x98)
				{
					if (sw2 == 0x02) _ret = "ERROR|No PIN defined.";
					else if (sw2 == 0x04) _ret = "ERROR|Access conditions not satisfied, authentication failed.";
					else if (sw2 == 0x35) _ret = "ERROR|ASK RANDOM or GIVE RANDOM not executed.";
					else if (sw2 == 0x40) _ret = "PIN verification not successful.";
					else if (sw2 == 0x50) _ret = "ERROR|INCREASE or DECREASE could not be executed because a limit has been reached.";
					else if (sw2 == 0x62) _ret = "ERROR|Authentication Error, application specific (incorrect MAC).";
					else _ret = "ERROR|PIN Error";
				}

				if (sw1 == 0x99)
				{
					if (sw2 == 0x00) _ret = "WARNING|1 PIN try left";
					else if (sw2 == 0x04) _ret = "WARNING|PIN not succesfully verified, 1 PIN try left";
					else if (sw2 == 0x85) _ret = "ERROR|Wrong status – Cardholder lock";
					else if (sw2 == 0x86) _ret = "ERROR|Missing privilege";
					else if (sw2 == 0x87) _ret = "ERROR|PIN is not installed";
					else if (sw2 == 0x88) _ret = "ERROR|Wrong status – R-MAC state";
					else _ret = "ERROR|PIN Error Unknown";
				}

				if (sw1 == 0x9A)
				{
					if (sw2 == 0x00) _ret = "WARNING|2 PIN try left";
					else if (sw2 == 0x04) _ret = "WARNING|PIN not succesfully verified, 2 PIN try left";
					else if (sw2 == 0x071) _ret = "ERROR|Wrong parameter value – Double agent AID";
					else if (sw2 == 0x072) _ret = "ERROR|Wrong parameter value – Double agent Type";
					else _ret = "ERROR|PIN Error Unknown";
				}

				if (sw1 == 0x9D)
				{
					if (sw2 == 0x05) _ret = "ERROR|Incorrect certificate type";
					else if (sw2 == 0x07) _ret = "ERROR|Incorrect session data size";
					else if (sw2 == 0x08) _ret = "ERROR|Incorrect DIR file record size";
					else if (sw2 == 0x09) _ret = "ERROR|Incorrect FCI record size";
					else if (sw2 == 0x0A) _ret = "ERROR|Incorrect code size";
					else if (sw2 == 0x10) _ret = "ERROR|Insufficient memory to load application";
					else if (sw2 == 0x11) _ret = "ERROR|Invalid AID";
					else if (sw2 == 0x12) _ret = "ERROR|Duplicate AID";
					else if (sw2 == 0x13) _ret = "ERROR|Application previously loaded";
					else if (sw2 == 0x14) _ret = "ERROR|Application history list full";
					else if (sw2 == 0x15) _ret = "ERROR|Application not open";
					else if (sw2 == 0x17) _ret = "ERROR|Invalid offset";
					else if (sw2 == 0x18) _ret = "ERROR|Application already loaded";
					else if (sw2 == 0x19) _ret = "ERROR|Invalid certificate";
					else if (sw2 == 0x1A) _ret = "ERROR|Invalid signature";
					else if (sw2 == 0x1B) _ret = "ERROR|Invalid KTU";
					else if (sw2 == 0x1D) _ret = "ERROR|MSM controls not set";
					else if (sw2 == 0x1E) _ret = "ERROR|Application signature does not exist";
					else if (sw2 == 0x1F) _ret = "ERROR|KTU does not exist";
					else if (sw2 == 0x20) _ret = "ERROR|Application not loaded";
					else if (sw2 == 0x21) _ret = "ERROR|Invalid Open command data length";
					else if (sw2 == 0x30) _ret = "ERROR|Check data parameter is incorrect (invalid start address)";
					else if (sw2 == 0x31) _ret = "ERROR|Check data parameter is incorrect(invalid length)";
					else if (sw2 == 0x32) _ret = "ERROR|Check data parameter is incorrect(illegal memory check area)";
					else if (sw2 == 0x40) _ret = "ERROR|Invalid MSM Controls ciphertext";
					else if (sw2 == 0x41) _ret = "ERROR|MSM controls already set";
					else if (sw2 == 0x42) _ret = "ERROR|Set MSM Controls data length less than 2 bytes";
					else if (sw2 == 0x43) _ret = "ERROR|Invalid MSM Controls data length";
					else if (sw2 == 0x44) _ret = "ERROR|Excess MSM Controls ciphertext";
					else if (sw2 == 0x45) _ret = "ERROR|Verification of MSM Controls data failed";
					else if (sw2 == 0x50) _ret = "ERROR|Invalid MCD Issuer production ID";
					else if (sw2 == 0x51) _ret = "ERROR|Invalid MCD Issuer ID";
					else if (sw2 == 0x52) _ret = "ERROR|Invalid set MSM controls data date";
					else if (sw2 == 0x53) _ret = "ERROR|Invalid MCD number";
					else if (sw2 == 0x54) _ret = "ERROR|Reserved field error";
					else if (sw2 == 0x55) _ret = "ERROR|Reserved field error";
					else if (sw2 == 0x56) _ret = "ERROR|Reserved field error";
					else if (sw2 == 0x57) _ret = "ERROR|Reserved field error";
					else if (sw2 == 0x60) _ret = "ERROR|MAC verification failed";
					else if (sw2 == 0x61) _ret = "ERROR|Maximum number of unblocks reached";
					else if (sw2 == 0x62) _ret = "ERROR|Card was not blocked";
					else if (sw2 == 0x63) _ret = "ERROR|Crypto functions not available";
					else if (sw2 == 0x64) _ret = "ERROR|No application loaded";
					else _ret = "ERROR|Unknown Error";
				}

				if (sw1 == 0x9E)
				{
					if (sw2 == 0x00) _ret = "WARNING|PIN not installed";
					else if (sw2 == 0x04) _ret = "WARNING|PIN not succesfully verified, PIN not installed";
					else _ret = "ERROR|Unknown";
				}

				if (sw1 == 0x9F)
				{
					if (sw2 == 0x00) _ret = "WARNING|PIN blocked and Unblock Try Counter is 3";
					else if (sw2 == 0x04) _ret = "WARNING|PIN not succesfully verified, PIN blocked and Unblock Try Counter is 3";
					else _ret = "ERROR|Unknown";
				}

				return _ret;
			}
		}

		public override string ToString()
		{
			return String.Format("Status: {{ SW:'{0}', SW1:{1}, SW2:{2}, Des:'{3}' }}", this.hex_value, this.sw1, this.sw2, this.descripcion);
		}

		/// <summary>Convierte un Array de Bytes en una cadena Hexadecimal.</summary>
		/// <param name="bytes">Array de Bytes a Convertir</param>
		/// <param name="lenght">Longitud del Array a Tomar</param>
		/// <param name="bSpace">'true'= Agrega Espacios entre los pares de bytes.</param>
		private string ByteArrayToHex(byte[] bytes, int lenght, bool bSpace = true)
		{
			string text2 = "";
			for (short num1 = 0; num1 < lenght; num1 = (short)(num1 + 1))
			{
				short num2 = bytes[num1];

				text2 = text2 + System.Convert.ToString(num2, 16).PadLeft(2, '0');

				if (bSpace)
				{
					text2 = text2 + " ";
				}
			}
			return text2.ToUpper().Trim();
		}
	}

	public class APDU_Return
	{
		public APDU_Return(int _ret)
		{
			this.code = _ret;
			this.hex_value = System.Convert.ToString(_ret, 16).PadLeft(2, '0');
			this.descripcion = ((ReturnCodes)_ret).ToString();
		}

		public int code { get; set; }
		public string hex_value { get; set; }
		public string descripcion { get; set; }

		public override string ToString()
		{
			return String.Format("Return: {{ Code:'{0}', Des:'{1}' }}", this.hex_value, this.descripcion );
		}

		private enum ReturnCodes : uint
		{
			COMMAND_EXECUTED = 0x0,
			//'Errors
			SCARD_E_CANCELLED = 0x80100002,
			SCARD_E_INVALID_HANDLE = 0x80100003,
			SCARD_E_INVALID_PARAMETER = 0x80100004,
			SCARD_E_INVALID_TARGET = 0x80100005,
			SCARD_E_NO_MEMORY = 0x80100006,
			SCARD_F_WAITED_TOO_LONG = 0x80100007,
			SCARD_E_INSUFFICIENT_BUFFER = 0x80100008,
			SCARD_E_UNKNOWN_READER = 0x80100009,
			SCARD_E_TIMEOUT = 0x8010000A,
			SCARD_E_SHARING_VIOLATION = 0x8010000B,
			SCARD_E_NO_SMARTCARD = 0x8010000C,
			SCARD_E_UNKNOWN_CARD = 0x8010000D,
			SCARD_E_CANT_DISPOSE = 0x8010000E,
			SCARD_E_PROTO_MISMATCH = 0x8010000F,
			SCARD_E_NOT_READY = 0x80100010,
			SCARD_E_INVALID_VALUE = 0x80100011,
			SCARD_E_SYSTEM_CANCELLED = 0x80100012,
			SCARD_E_INVALID_ATR = 0x80100015,
			SCARD_E_NOT_TRANSACTED = 0x80100016,
			SCARD_E_READER_UNAVAILABLE = 0x80100017,
			SCARD_E_PCI_TOO_SMALL = 0x80100019,
			SCARD_E_READER_UNSUPPORTED = 0x8010001A,
			SCARD_E_DUPLICATE_READER = 0x8010001B,
			SCARD_E_CARD_UNSUPPORTED = 0x8010001C,
			SCARD_E_NO_SERVICE = 0x8010001D,
			SCARD_E_SERVICE_STOPPED = 0x8010001E,
			SCARD_E_UNEXPECTED = 0x8010001F,
			SCARD_E_ICC_INSTALLATION = 0x80100020,
			SCARD_E_ICC_CREATEORDER = 0x80100021,
			SCARD_E_DIR_NOT_FOUND = 0x80100023,
			SCARD_E_FILE_NOT_FOUND = 0x80100024,
			SCARD_E_NO_DIR = 0x80100025,
			SCARD_E_NO_FILE = 0x80100026,
			SCARD_E_NO_ACCESS = 0x80100027,
			SCARD_E_WRITE_TOO_MANY = 0x80100028,
			SCARD_E_BAD_SEEK = 0x80100029,
			SCARD_E_INVALID_CHV = 0x8010002A,
			SCARD_E_UNKNOWN_RES_MNG = 0x8010002B,
			SCARD_E_NO_SUCH_CERTIFICATE = 0x8010002C,
			SCARD_E_CERTIFICATE_UNAVAILABLE = 0x8010002D,
			SCARD_E_NO_READERS_AVAILABLE = 0x8010002E,
			SCARD_E_COMM_DATA_LOST = 0x8010002F,
			SCARD_E_NO_KEY_CONTAINER = 0x80100030,
			SCARD_E_SERVER_TOO_BUSY = 0x80100031,
			SCARD_E_UNSUPPORTED_FEATURE = 0x8010001F,
			//'The F...not sure
			SCARD_F_INTERNAL_ERROR = 0x80100001,
			SCARD_F_COMM_ERROR = 0x80100013,
			SCARD_F_UNKNOWN_ERROR = 0x80100014,
			//'The W...not sure
			SCARD_W_UNSUPPORTED_CARD = 0x80100065,
			SCARD_W_UNRESPONSIVE_CARD = 0x80100066,
			SCARD_W_UNPOWERED_CARD = 0x80100067,
			SCARD_W_RESET_CARD = 0x80100068,
			SCARD_W_REMOVED_CARD = 0x80100069,
			SCARD_W_SECURITY_VIOLATION = 0x8010006A,
			SCARD_W_WRONG_CHV = 0x8010006B,
			SCARD_W_CHV_BLOCKED = 0x8010006C,
			SCARD_W_EOF = 0x8010006D,
			SCARD_W_CANCELLED_BY_USER = 0x8010006E,
			SCARD_W_CARD_NOT_AUTHENTICATED = 0x8010006F,
			SCARD_W_INSERTED_CARD = 0x8010006A,

			SCARD_NOT_PRESENT = 0x6
		}
	}

	/// <summary>Plantilla TLV para extraer los Datos Biograficos de la Cedula Uruguaya.</summary>
	public class TLV_Cedula
	{
		/*1F 01 0E  PrimerApellido, Len:0E= 14bytes
			 43 48 41 43 C3 93 4E 20 52 41 4E 47 45 4C  	---> 'CHACÓ?N RANGEL'
		* 1F 02 00 SegundoApellido, Len:00=0bytes
		* 1F 03 08 Nombres (1º y 2º), Len:08=8bytes
			 4A 48 4F 4C 4C 4D 41 4E    			---> 'JHOLLMAN'
		* 1F 04 03 Nacionalidad, Len:03
			 43 4F 4C   					---> 'COL'
		* 1F 05 08 Fec.Nacimiento (DDMMAAAA), Len:08
			 31 38 30 34 31 39 37 35    			---> '18041975'
		* 1F 06 0D Lugar Nacimiento (Ciudad/Pais), Len:0D= 13bytes
			 53 41 4E 54 41 4E 44 45 52 2F 43 4F 4C   		---> 'SANTANDER/COL'
		* 1F 07 08 Nro.Cedula
			 35 34 39 36 39 38 34 36 				---> '54969846'
		* 1F 08 04 Fec.Expedicion (DDMMYYYY)
			 14 08 20 16  					---> '14082016' 
		* 1F 09 08 Fec.Vencimiento (DDMMYYYY)
			 33 30 30 36 32 30 31 39 				---> '30062019'
		* 1F 0A 1B Observaciones Len:1B=27bytes
			 52 45 53 49 44 45 4E 54 45 20 4C 45 47 41 4C 20 44 4E 4D 20 31 31 31 34 2F 30 35 
			 'RESIDENTE LEGAL DNM 1114/05'   */

		public TLV_Cedula(byte[] bytes)
		{			
			if (bytes != null && bytes.Length > 0)
			{
				this.TLV_Bytes = bytes;

				//1. Extraer el Primer Apellido (Tag '1F 01'):
				int Index = 2;			//<- Index del 1º Tag
				int Len = bytes[Index];	//<- Longitud del Dato
				byte[] Buffer = new byte[Len];
				if (Len > 0)
				{
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.apellido_1 = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
					}
				}

				//2. Extraer el Segundo Apellido (Tag '1F 02'):
				Index += Len +3;	//<- Index del 2º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.apellido_2 = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
					}
				}
				else { this.apellido_2 = string.Empty; }

				//3. Extraer los Nombres (Tag '1F 03'):
				Index += Len + 3;   //<- Index del 3º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.nombres = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
					}
				}

				//4. Extraer la Nacionalidad (Tag '1F 04'):
				Index += Len + 3;   //<- Index del 4º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.nacionalidad = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
					}
				}

				//5. Extraer la Fecha de Nacimiento (Tag '1F 05'):
				Index += Len + 3;   //<- Index del 5º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						string _ret = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
						this.fecha_nacimiento = new DateTime(
							Convert.ToInt32(_ret.Substring(Len - 4, 4)),
							Convert.ToInt32(_ret.Substring(Len - 6, 2)),
							Convert.ToInt32(_ret.Substring(0, 2))
						);
					}
				}

				//6. Extraer el Lugar de Nacimiento (Tag '1F 06'):
				Index += Len + 3;   //<- Index del 6º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.lugar_nacimiento = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
					}
				}

				//7. Extraer el Numero de Cedula (Tag '1F 07'):
				Index += Len + 3;   //<- Index del 7º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.nro_cedula = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
					}
				}

				//8. Extraer la Fecha de Expedicion (Tag '1F 07'):
				Index += Len + 3;   //<- Index del 8º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.fecha_expedicion = new DateTime(
							Convert.ToInt32(string.Format("{0}{1}",
								System.Convert.ToString(Buffer[Len - 2], 16).PadLeft(2, '0'),
								System.Convert.ToString(Buffer[Len - 1], 16).PadLeft(2, '0'))),
							Convert.ToInt32(System.Convert.ToString(Buffer[1], 16).PadLeft(2, '0')),
							Convert.ToInt32(System.Convert.ToString(Buffer[0], 16).PadLeft(2, '0'))
						);
					}
				}

				//9. Extraer la Fecha de Vencimiento (Tag '1F 09'):
				Index += Len + 3;   //<- Index del 9º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						string _ret = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
						this.fecha_vencimiento = new DateTime(
							Convert.ToInt32(_ret.Substring(Len - 4, 4)),
							Convert.ToInt32(_ret.Substring(Len - 6, 2)),
							Convert.ToInt32(_ret.Substring(0, 2))
						);
					}
				}

				//10. Extraer las Observaciones (Tag '1F 0A'):
				Index += Len + 3;   //<- Index del 10º Tag
				Len = bytes[Index]; //<- Longitud del Dato
				if (Len > 0)
				{
					Buffer = new byte[Len];
					Array.Copy(bytes, Index + 1, Buffer, 0, Len);
					if (Buffer != null && Buffer.Length > 0)
					{
						this.observaciones = System.Text.Encoding.UTF8.GetString(Buffer, 0, Buffer.Length);
					}
				}
				else { this.observaciones = string.Empty; }
			}
		}

		public byte[] TLV_Bytes { get; set; }

		public string apellido_1 { get; set; }
		public string apellido_2 { get; set; }
		public string nombres { get; set; }
		public string nacionalidad { get; set; }

		public DateTime fecha_nacimiento { get; set; }
		public string lugar_nacimiento { get; set; }
		public string nro_cedula { get; set; }

		public DateTime fecha_expedicion { get; set; }
		public DateTime fecha_vencimiento { get; set; }

		public string observaciones { get; set; }

		public byte[] foto_persona { get; set; }
	}

}
