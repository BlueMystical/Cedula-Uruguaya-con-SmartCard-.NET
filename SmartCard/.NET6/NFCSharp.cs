using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

/* LECTOR DE TARJETAS INTELIGENTES (SmartCard) con NFC  *
 * Actualizado a .NET 6.0 por Jhollman Chacon - Nov/2023
 * ---------------------------------------------------------- 
 * MODO DE USO:

// el Lector a usar:
private NFCReader _Reader = null;
[...]
#region Iniciar el Lector de Tarjetas

NFCHandler.Init();

// Enumera la Lista de Lectores de Tarjetas conectados al PC:
cboReaders.DataSource = NFCHandler.GetReaders();

//Instancia e Inicializa el Primer lector encontrado:
if (cboReaders.Items.Count > 0) cboReaders.SelectedIndex = 0;
_Reader = NFCHandler.Readers[cboReaders.SelectedIndex];
_Reader.TagFound += _Reader_TagFound;	//<- Tarjeta Insertada
_Reader.TagLost += _Reader_TagLost;		//<- Tarjeta Retirada
_Reader.StartPolling();	

#endregion 

[...]

private void Form1_FormClosing(object sender, FormClosingEventArgs e)
{
	if (_Reader != null) _Reader.StopPolling();
	NFCHandler.Release();
}

 */
namespace SmartCardReader
{
	/// <summary>Evento que detecta la Insersion de una Tarjeta.</summary>
	/// <param name="Tag">Datos de la Tarjeta</param>
	public delegate void TagFoundHandler(NFCTag Tag);

	/// <summary>Evento que detecta la Eyeccion de una Tarjeta.</summary>
	public delegate void TagLostHandler();

	/// <summary>Este es el Manejador del Lector de Tarjetas.</summary>
	public static class NFCHandler
	{
		private static IntPtr hContext = IntPtr.Zero;

		public static int DefaultReader = 0;
		public static bool IsInitialized { get { return hContext != IntPtr.Zero; } }

		private static List<NFCReader> readers = new List<NFCReader>();
		public static readonly ReadOnlyCollection<NFCReader> Readers = readers.AsReadOnly();


		static NFCHandler()
		{
		}

		public static void Init()
		{
			if (IsInitialized) Release();

			int retCode;

			// Get context
			retCode = SCW.SCardEstablishContext(SCW.SCARD_SCOPE_USER, 0, 0, out hContext);

			if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed extablishing context: " + SCW.GetScardErrMsg(retCode));

			// Get PC/SC readers available
			int pcchReaders = 0;
			retCode = SCW.SCardListReaders(hContext, null, null, ref pcchReaders);

			if (retCode == SCW.SCARD_E_NO_READERS_AVAILABLE) return;
			else if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed listing readers: " + SCW.GetScardErrMsg(retCode));

			// Fill reader list
			readers.Clear();
			byte[] ReadersList = new byte[pcchReaders];
			retCode = SCW.SCardListReaders(hContext, null, ReadersList, ref pcchReaders);

			// Convert reader buffer to string
			int idxBytes = 0, idxNames = 0;
			string rdrName = "";
			string[] readersNames = new string[pcchReaders];
			while (ReadersList[idxBytes] != 0)
			{
				while (ReadersList[idxBytes] != 0)
				{
					rdrName = rdrName + (char)ReadersList[idxBytes];
					idxBytes++;
				}

				if (rdrName.StartsWith("DUALi"))
					readers.Add(new Readers.DualiReader(hContext, rdrName));
				else if (rdrName.StartsWith("ACS"))
					readers.Add(new Readers.ACSReader(hContext, rdrName));
				else if (rdrName.StartsWith("SCM") || (rdrName.StartsWith("Identive") && rdrName.Contains("Contactless")))
					readers.Add(new Readers.IdentiveReader(hContext, rdrName));

				rdrName = "";
				idxBytes++;
				idxNames++;
			}

			DefaultReader = 0;
		}

		public static void Release()
		{
			if (IsInitialized)
			{
				int retCode = SCW.SCardReleaseContext(hContext);

				if (retCode != SCW.SCARD_S_SUCCESS)
					throw new Exception("Failed release!");

				hContext = IntPtr.Zero;
				readers.Clear();
			}
		}

		/// <summary>Returns a List with all available Readers.</summary>
		public static List<string> GetReaders()
		{
			List<string> _ret = null;
			if (readers != null)
			{
				_ret = new List<string>();
                foreach (NFCReader rdr in readers)
                {
					_ret.Add(rdr.Name);
				}
            }
			return _ret;
		}
	}

	/// <summary>Este es un Lector especifico de Tarjetas.</summary>
	public abstract class NFCReader
	{
		public NFCReader(IntPtr Handle, string ReaderName)
		{
			Name = ReaderName;
			hContext = Handle;

			pollerThread = new Thread(Poller);
		}

		public readonly string Name;
		protected readonly IntPtr hContext;

		private Thread pollerThread;

		public abstract NFCTag Connect();

		protected byte[] GetATR(IntPtr Handle, int Proto)
		{
			string rName = String.Empty;
			int rLenght = 0, ATRLen = 33, dwState = 0;
			byte[] ATRBytes = new byte[32];

			int retCode = SCW.SCardStatus(Handle, rName,
				ref rLenght, ref dwState, ref Proto, ref ATRBytes[0], ref ATRLen);

			if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed querying tag status: " + SCW.GetScardErrMsg(retCode));

			byte[] ATR = new byte[ATRLen];
			Array.Copy(ATRBytes, ATR, ATRLen);
			return ATR;
		}

		protected NFCTag BuildTag(IntPtr Handle, int Proto, NFCReader Reader, byte[] ATR)
		{
			switch (ParseATR(ATR))
			{
				case TagType.MifareUltralightFamily:
					return new Tags.Ultralight(Handle, Proto, Reader, ATR);

				default:
					return new Tags.Unknown(Handle, Proto, Reader, ATR);
			}
		}

		internal abstract byte[] ParseUID(IntPtr Handle, int Proto);
		public abstract TagType ParseATR(byte[] bATR);

		public abstract string Test();

		public abstract void LoadKey(IntPtr Handle, int Proto, KeyTypes KeyType, byte[] KeyData);
		public abstract void Authenticate(IntPtr Handle, int Proto, KeyTypes KeyType, byte Sector);

		public abstract byte[] Read(IntPtr Handle, int Proto, byte Page);
		public abstract void Write(IntPtr Handle, int Proto, byte Page, byte[] Data);

		public abstract byte[] Transmit(IntPtr Handle, int Proto, byte[] CmdBytes);

		#region Polling Ops

		private DateTime LastPoll = DateTime.Now;
		private SCW.SmartcardState _state;

		/// <summary>Inicia un proceso que chequea los cambios en el Lector.</summary>
		public void StartPolling()
		{
			if (!pollerThread.IsAlive)
			{
				LastPoll = DateTime.Now;
				pollerThread.Start();
			}
		}

		/// <summary>Detiene la deteccion de eventos del lector</summary>
		public void StopPolling()
		{
			stopPollingSignal = true;
			//pollerThread.Abort();
		}

		

		protected void Poller()
		{
			SCW.SCARD_READERSTATE[] State = new SCW.SCARD_READERSTATE[1];
			State[0].RdrName = this.Name;
			State[0].UserData = IntPtr.Zero;
			State[0].RdrCurrState = SCW.SCARD_STATE_UNKNOWN;

			int retCode;

			retCode = SCW.SCardGetStatusChange(hContext, 100, State, 1);
			if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed initial get status change: " + SCW.GetScardErrMsg(retCode));

			State[0].RdrCurrState = State[0].RdrEventState;			

			while (!stopPollingSignal)
			{
				retCode = SCW.SCardGetStatusChange(hContext, 1000, State, 1);

				if (retCode == SCW.SCARD_E_TIMEOUT)
					continue;

				for (int i = 0; i <= State.Length - 1; i++)
				{
					//Check if the state changed from the last time.
					if ((State[i].RdrEventState & SCW.SCARD_STATE_CHANGED) == SCW.SCARD_STATE_CHANGED)
					{
						//Check what changed.
						this._state = SCW.SmartcardState.None;
						if (   (State[i].RdrEventState & SCW.SCARD_STATE_PRESENT) == SCW.SCARD_STATE_PRESENT
							&& (State[i].RdrCurrState & SCW.SCARD_STATE_PRESENT) != SCW.SCARD_STATE_PRESENT)
						{
							//The card was inserted.                            
							this._state = SCW.SmartcardState.Inserted;

							if (TagFound != null)
							{
								NFCTag tag = Connect();
								if (tag != null)
								{
									foreach (Delegate d in TagFound.GetInvocationList())
									{
										ISynchronizeInvoke? syncer = d.Target as ISynchronizeInvoke;
										if (syncer != null)
											syncer.BeginInvoke(d, new NFCTag[] { tag });
										else
											d.DynamicInvoke(tag);
									}
									LastPoll = DateTime.Now;
								}
							}							
						}
						else if ((State[i].RdrEventState & SCW.SCARD_STATE_EMPTY) == SCW.SCARD_STATE_EMPTY
							  && (State[i].RdrCurrState & SCW.SCARD_STATE_EMPTY) != SCW.SCARD_STATE_EMPTY)
						{
							//The card was ejected.
							this._state = SCW.SmartcardState.Ejected;

							if (TagLost != null)
							{
								foreach (Delegate d in TagLost.GetInvocationList())
								{
									ISynchronizeInvoke? syncer = d.Target as ISynchronizeInvoke;
									if (syncer != null)
										syncer.BeginInvoke(d, null);
									else
										d.DynamicInvoke();
								}
							}
						}

						//Update the current state for the next time they are checked.
						State[i].RdrCurrState = State[i].RdrEventState;
					}
				}
			}
		}

		protected volatile bool stopPollingSignal = false;

		/// <summary>Evento que detecta la Insersion de una Tarjeta.</summary>
		public event TagFoundHandler TagFound;

		/// <summary>Evento que detecta la Eyeccion de una Tarjeta.</summary>
		public event TagLostHandler TagLost;

		#endregion
	}

	internal class SCW
	{
		// APIs de Windows para acceder al Lector

		[DllImport("winscard.dll")]
		public static extern int SCardEstablishContext(int dwScope, int pvReserved1, int pvReserved2, out IntPtr hContext);

		[DllImport("winscard.dll")]
		public static extern int SCardReleaseContext(IntPtr hContext);


		[DllImport("winscard.dll")]
		public static extern int SCardListReaders(IntPtr hContext, byte[] Groups, byte[] Readers, ref int pcchReaders);

		[DllImport("winscard.dll")]
		public static extern int SCardListReaderGroups(IntPtr hContext, ref string Groups, ref int pcchGroups);

		[DllImport("winscard.dll", CharSet = CharSet.Unicode)]
		public static extern int SCardGetStatusChange(IntPtr hContext, int dwTimeout, [In, Out] SCARD_READERSTATE[] rgReaderStates, int cReaders);


		[DllImport("winscard.dll")]
		public static extern int SCardConnect(IntPtr hContext, string ReaderName, int dwShareMode, int dwPrefProtocol, out IntPtr hCard, ref int ActiveProtocol);

		[DllImport("winscard.dll")]
		public static extern int SCardDisconnect(IntPtr hCard, int Disposition);


		[DllImport("winscard.dll")]
		public static extern int SCardStatus(IntPtr hCard, string ReaderName, ref int pcchReaderLen, ref int State, ref int Protocol, ref byte ATR, ref int ATRLen);

		[DllImport("winscard.dll")]
		public static extern int SCardState(IntPtr hCard, ref int State, ref int Protocol, ref byte ATR, ref int ATRSize);

		[DllImport("winscard.dll")]
		public static extern int SCardBeginTransaction(IntPtr hCard);

		[DllImport("winscard.dll")]
		public static extern int SCardEndTransaction(IntPtr hCard, int Disposition);


		[DllImport("winscard.dll")]
		public static extern int SCardTransmit(IntPtr hCard, ref SCARD_IO_REQUEST ioSendRequest, byte[] InBuff, int InBuffSize, ref SCARD_IO_REQUEST ioRecvRequest, byte[] OutBuff, out int OutBuffSize);


		[DllImport("winscard.dll")]
		public static extern int SCardControl(IntPtr hCard, int ControlCode, byte[] InBuffer, int InBufferSize, byte[] OutBuffer, int OutBufferSize, out int BytesReturned);

		[DllImport("winscard.dll")]
		public static extern int SCardLocateCards(IntPtr hContext, byte[] mszCards, SCARD_READERSTATE[] rgReaderStates, int cReaders);

		// STRUCTURES

		[StructLayout(LayoutKind.Sequential)]
		public struct SCARD_IO_REQUEST
		{
			public int dwProtocol;
			public int cbPciLength;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct SCARD_READERSTATE
		{
			[MarshalAs(UnmanagedType.LPTStr)]
			public string RdrName;
			public IntPtr UserData;
			public int RdrCurrState;
			public int RdrEventState;
			public int ATRLength;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
			public byte[] ATRBytes;
		}

		public const int SCARD_INFINITE_WAIT = -1;

		// CONTEXT SCOPE

		public const int SCARD_SCOPE_USER = 0;
		public const int SCARD_SCOPE_TERMINAL = 1;
		public const int SCARD_SCOPE_SYSTEM = 2;

		public const int SCARD_SHARE_EXCLUSIVE = 1;
		public const int SCARD_SHARE_SHARED = 2;
		public const int SCARD_SHARE_DIRECT = 3;

		// CARD STATES

		public const int SCARD_STATE_UNAWARE = 0x00;
		public const int SCARD_STATE_IGNORE = 0x01;
		public const int SCARD_STATE_CHANGED = 0x02;
		public const int SCARD_STATE_UNKNOWN = 0x04;
		public const int SCARD_STATE_UNAVAILABLE = 0x08;
		public const int SCARD_STATE_EMPTY = 0x10;
		public const int SCARD_STATE_PRESENT = 0x20;
		public const int SCARD_STATE_ATRMATCH = 0x40;
		public const int SCARD_STATE_EXCLUSIVE = 0x80;
		public const int SCARD_STATE_INUSE = 0x100;
		public const int SCARD_STATE_MUTE = 0x200;
		public const int SCARD_STATE_UNPOWERED = 0x400;

		// READER STATES

		//public const int SCARD_UNKNOWN    = 0;
		//public const int SCARD_ABSENT     = 1;
		//public const int SCARD_PRESENT    = 2;
		//public const int SCARD_SWALLOWED  = 3;
		//public const int SCARD_POWERED    = 4;
		//public const int SCARD_NEGOTIABLE = 5;
		//public const int SCARD_SPECIFIC   = 6;

		// DISPOSITION

		public const int SCARD_LEAVE_CARD = 0;
		public const int SCARD_RESET_CARD = 1;
		public const int SCARD_UNPOWER_CARD = 2;
		public const int SCARD_EJECT_CARD = 3;

		// ERROR CODES

		public const int SCARD_S_SUCCESS = 0;

		public const int SCARD_F_INTERNAL_ERROR = unchecked((int)0x80100001);
		public const int SCARD_E_CANCELLED = unchecked((int)0x80100002);
		public const int SCARD_E_INVALID_HANDLE = unchecked((int)0x80100003);
		public const int SCARD_E_INVALID_PARAMETER = unchecked((int)0x80100004);
		public const int SCARD_E_INVALID_TARGET = unchecked((int)0x80100005);
		public const int SCARD_E_NO_MEMORY = unchecked((int)0x80100006);
		public const int SCARD_F_WAITED_TOO_LONG = unchecked((int)0x80100007);
		public const int SCARD_E_INSUFFICIENT_BUFFER = unchecked((int)0x80100008);
		public const int SCARD_E_UNKNOWN_READER = unchecked((int)0x80100009);
		public const int SCARD_E_TIMEOUT = unchecked((int)0x8010000A);
		public const int SCARD_E_SHARING_VIOLATION = unchecked((int)0x8010000B);
		public const int SCARD_E_NO_SMARTCARD = unchecked((int)0x8010000C);
		public const int SCARD_E_UNKNOWN_CARD = unchecked((int)0x8010000D);
		public const int SCARD_E_CANT_DISPOSE = unchecked((int)0x8010000E);
		public const int SCARD_E_PROTO_MISMATCH = unchecked((int)0x8010000F);
		public const int SCARD_E_NOT_READY = unchecked((int)0x80100010);
		public const int SCARD_E_INVALID_VALUE = unchecked((int)0x80100011);
		public const int SCARD_E_SYSTEM_CANCELLED = unchecked((int)0x80100012);
		public const int SCARD_F_COMM_ERROR = unchecked((int)0x80100013);
		public const int SCARD_F_UNKNOWN_ERROR = unchecked((int)0x80100014);
		public const int SCARD_E_INVALID_ATR = unchecked((int)0x80100015);
		public const int SCARD_E_NOT_TRANSACTED = unchecked((int)0x80100016);
		public const int SCARD_E_READER_UNAVAILABLE = unchecked((int)0x80100017);
		public const int SCARD_P_SHUTDOWN = unchecked((int)0x80100018);
		public const int SCARD_E_PCI_TOO_SMALL = unchecked((int)0x80100019);
		public const int SCARD_E_READER_UNSUPPORTED = unchecked((int)0x8010001A);
		public const int SCARD_E_DUPLICATE_READER = unchecked((int)0x8010001B);
		public const int SCARD_E_CARD_UNSUPPORTED = unchecked((int)0x8010001C);
		public const int SCARD_E_NO_SERVICE = unchecked((int)0x8010001D);
		public const int SCARD_E_SERVICE_STOPPED = unchecked((int)0x8010001E);
		public const int SCARD_E_UNEXPECTED = unchecked((int)0x8010001F);
		public const int SCARD_E_ICC_INSTALLATION = unchecked((int)0x80100020);
		public const int SCARD_E_ICC_CREATEORDER = unchecked((int)0x80100021);
		public const int SCARD_E_UNSUPPORTED_FEATURE = unchecked((int)0x80100022);
		public const int SCARD_E_DIR_NOT_FOUND = unchecked((int)0x80100023);
		public const int SCARD_E_FILE_NOT_FOUND = unchecked((int)0x80100024);
		public const int SCARD_E_NO_DIR = unchecked((int)0x80100025);
		public const int SCARD_E_NO_FILE = unchecked((int)0x80100026);
		public const int SCARD_E_NO_ACCESS = unchecked((int)0x80100027);
		public const int SCARD_E_WRITE_TOO_MANY = unchecked((int)0x80100028);
		public const int SCARD_E_BAD_SEEK = unchecked((int)0x80100029);
		public const int SCARD_E_INVALID_CHV = unchecked((int)0x8010002A);
		public const int SCARD_E_UNKNOWN_RES_MNG = unchecked((int)0x8010002B);
		public const int SCARD_E_NO_SUCH_CERTIFICATE = unchecked((int)0x8010002C);
		public const int SCARD_E_CERTIFICATE_UNAVAILABLE = unchecked((int)0x8010002D);
		public const int SCARD_E_NO_READERS_AVAILABLE = unchecked((int)0x8010002E);
		public const int SCARD_E_COMM_DATA_LOST = unchecked((int)0x8010002F);
		public const int SCARD_E_NO_KEY_CONTAINER = unchecked((int)0x80100030);
		public const int SCARD_E_SERVER_TOO_BUSY = unchecked((int)0x80100031);
		public const int SCARD_W_UNSUPPORTED_CARD = unchecked((int)0x80100065);
		public const int SCARD_W_UNRESPONSIVE_CARD = unchecked((int)0x80100066);
		public const int SCARD_W_UNPOWERED_CARD = unchecked((int)0x80100067);
		public const int SCARD_W_RESET_CARD = unchecked((int)0x80100068);
		public const int SCARD_W_REMOVED_CARD = unchecked((int)0x80100069);
		public const int SCARD_W_SECURITY_VIOLATION = unchecked((int)0x8010006A);
		public const int SCARD_W_WRONG_CHV = unchecked((int)0x8010006B);
		public const int SCARD_W_CHV_BLOCKED = unchecked((int)0x8010006C);
		public const int SCARD_W_EOF = unchecked((int)0x8010006D);
		public const int SCARD_W_CANCELLED_BY_USER = unchecked((int)0x8010006E);
		public const int SCARD_W_CARD_NOT_AUTHENTICATED = unchecked((int)0x8010006F);
		public const int SCARD_W_CACHE_ITEM_NOT_FOUND = unchecked((int)0x80100070);
		public const int SCARD_W_CACHE_ITEM_STALE = unchecked((int)0x80100071);
		public const int SCARD_W_CACHE_ITEM_TOO_BIG = unchecked((int)0x80100072);

		//  PROTOCOLS

		public const int SCARD_PROTOCOL_UNDEFINED = 0x00;       // There is no active protocol.
		public const int SCARD_PROTOCOL_T0 = 0x01;       // T=0 is the active protocol.
		public const int SCARD_PROTOCOL_T1 = 0x02;       // T=1 is the active protocol.
		public const int SCARD_PROTOCOL_RAW = 0x10000;    // Raw is the active protocol.
														  //public const int SCARD_PROTOCOL_DEFAULT   = 0x80000000; // Use implicit PTS.

		//CardState enumeration, used by the PC/SC function SCardGetStatusChanged.    
		public enum CardState
		{
			//Unaware
			None = 0,
			Ignore = 1,
			Changed = 2,
			Unknown = 4,
			Unavailable = 8,
			Empty = 16,
			Present = 32,
			AttributeMatch = 64,
			Exclusive = 128,
			InUse = 256,
			Mute = 512,
			Unpowered = 1024
		}

		/// <summary>Codigos para el Estado de una Tarjeta en el Lector.</summary>
		public enum SmartcardState
		{
			None = 0,
			Inserted = 1,
			Ejected = 2
		}

		// HELPERS

		public static string GetScardErrMsg(int ReturnCode)
		{
			switch (ReturnCode)
			{
				case SCARD_E_CANCELLED:
					return ("The action was canceled by an SCardCancel request.");
				case SCARD_E_CANT_DISPOSE:
					return ("The system could not dispose of the media in the requested manner.");
				case SCARD_E_CARD_UNSUPPORTED:
					return ("The smart card does not meet minimal requirements for support.");
				case SCARD_E_DUPLICATE_READER:
					return ("The reader driver didn't produce a unique reader name.");
				case SCARD_E_INSUFFICIENT_BUFFER:
					return ("The data buffer for returned data is too small for the returned data.");
				case SCARD_E_INVALID_ATR:
					return ("An ATR string obtained from the registry is not a valid ATR string.");
				case SCARD_E_INVALID_HANDLE:
					return ("The supplied handle was invalid.");
				case SCARD_E_INVALID_PARAMETER:
					return ("One or more of the supplied parameters could not be properly interpreted.");
				case SCARD_E_INVALID_TARGET:
					return ("Registry startup information is missing or invalid.");
				case SCARD_E_INVALID_VALUE:
					return ("One or more of the supplied parameter values could not be properly interpreted.");
				case SCARD_E_NOT_READY:
					return ("The reader or card is not ready to accept commands.");
				case SCARD_E_NOT_TRANSACTED:
					return ("An attempt was made to end a non-existent transaction.");
				case SCARD_E_NO_MEMORY:
					return ("Not enough memory available to complete this command.");
				case SCARD_E_NO_SERVICE:
					return ("The smart card resource manager is not running.");
				case SCARD_E_NO_SMARTCARD:
					return ("The operation requires a smart card, but no smart card is currently in the device.");
				case SCARD_E_PCI_TOO_SMALL:
					return ("The PCI receive buffer was too small.");
				case SCARD_E_PROTO_MISMATCH:
					return ("The requested protocols are incompatible with the protocol currently in use with the card.");
				case SCARD_E_READER_UNAVAILABLE:
					return ("The specified reader is not currently available for use.");
				case SCARD_E_READER_UNSUPPORTED:
					return ("The reader driver does not meet minimal requirements for support.");
				case SCARD_E_SERVICE_STOPPED:
					return ("The smart card resource manager has shut down.");
				case SCARD_E_SHARING_VIOLATION:
					return ("The smart card cannot be accessed because of other outstanding connections.");
				case SCARD_E_SYSTEM_CANCELLED:
					return ("The action was canceled by the system, presumably to log off or shut down.");
				case SCARD_E_TIMEOUT:
					return ("The user-specified timeout value has expired.");
				case SCARD_E_UNKNOWN_CARD:
					return ("The specified smart card name is not recognized.");
				case SCARD_E_UNKNOWN_READER:
					return ("The specified reader name is not recognized.");
				case SCARD_F_COMM_ERROR:
					return ("An internal communications error has been detected.");
				case SCARD_F_INTERNAL_ERROR:
					return ("An internal consistency check failed.");
				case SCARD_F_UNKNOWN_ERROR:
					return ("An internal error has been detected, but the source is unknown.");
				case SCARD_F_WAITED_TOO_LONG:
					return ("An internal consistency timer has expired.");
				case SCARD_S_SUCCESS:
					return ("No error was encountered.");
				case SCARD_W_REMOVED_CARD:
					return ("The smart card has been removed, so that further communication is not possible.");
				case SCARD_W_RESET_CARD:
					return ("The smart card has been reset, so any shared state information is invalid.");
				case SCARD_W_UNPOWERED_CARD:
					return ("Power has been removed from the smart card, so that further communication is not possible.");
				case SCARD_W_UNRESPONSIVE_CARD:
					return ("The smart card is not responding to a reset.");
				case SCARD_W_UNSUPPORTED_CARD:
					return ("The reader cannot communicate with the card, due to ATR string configuration conflicts.");
				case SCARD_E_COMM_DATA_LOST:
					return ("A communications error with the smart card has been detected.");
				default:
					return ("0x" + ReturnCode.ToString("X") + " " + ReturnCode + " ?");
			}
		}

		public static string errorStatusWord(string status)
		{
			string text3 = status;
			if (text3 == "90 00")
			{
				return "No Error";
			}
			if (text3 == "6A 81")
			{
				return "Function not supported ";
			}
			if (text3 == "6A 82")
			{
				return "File not found, addressed blocks or bytes do not exist ";
			}
			if (text3 == "6B 00")
			{
				return "Wrong P1, P2 parameters";
			}
			if (text3 == "6C XX")
			{
				return "Wrong Le, 0xXX is the correct value ";
			}
			if (text3 == "6D 00")
			{
				return "Estructura byte invalida";
			}
			if (text3 == "6E 00" || text3 == "6E00")
			{
				return "Clase invalida";
			}
			if (text3 == "6F 00")
			{
				return "Comando invalido";
			}
			if (text3 == "62 81")
			{
				return "WARNING: part of the returned data may be Corrupted ";
			}
			if (text3 == "62 82")
			{
				return "WARNING: end of file reached before Le bytes where read ";
			}
			if (text3 == "63 00")
			{
				return "Error de autenticacion";
			}
			if (text3 == "65 81")
			{
				return "Fallo el comando";
			}
			if (text3 == "65 91")
			{
				return "Status de seguridad no encontrado";
			}
			if (text3 == "67 00" || text3 == "6700")
			{
				return "Length incorrect ";
			}
			if (text3 == "68 00")
			{
				return "CLA byte incorrect ";
			}
			if (text3 == "69 81")
			{
				return "Command not supported ";
			}
			if (text3 == "69 82")
			{
				return "Security status not satisfied ";
			}
			if (text3 == "69 83")
			{
				return "Reader key not supported ";
			}
			if (text3 == "69 85")
			{
				return "Secured transmission not supported ";
			}
			if (text3 == "69 86")
			{
				return "Command not allowed";
			}
			if (text3 == "69 87")
			{
				return "Non volatile memory not available ";
			}
			if (text3 == "69 88")
			{
				return "Key number not valid ";
			}
			if (text3 == "69 89")
			{
				return "Key length not correct ";
			}

			return "Error Generico, Ver: @er".Replace("@er", status);
		}

	}

	public abstract class NFCTag : IDisposable
	{
		public NFCTag(IntPtr Handle, int Proto, NFCReader Reader, byte[] ATRbytes)
		{
			handle = Handle;
			proto = Proto;
			reader = Reader;

			// Get Tag ATR
			bATR = ATRbytes;
			ATR = bATR.BytesToHex();

			// Get Tag UID in different formats: bytes, string, numeric(long).
			bUID = reader.ParseUID(handle, proto);
			UID = bUID.BytesToHex();
			nUID = Int64.Parse(UID.Replace(" ", ""), System.Globalization.NumberStyles.HexNumber);

			ReaderName = Reader.Name;
			CardInfo = GetCardInfo();
			CardType = reader.ParseATR(bATR);
		}

		protected IntPtr handle;
		protected int proto;
		protected NFCReader reader;

		/// <summary>Nombre del lector usado</summary>
        public string ReaderName { get; set; }

		/// <summary>ATR de la tarjeta en formato de bytes.</summary>
		public readonly byte[] bATR;

		/// <summary>ATR de la tarjeta en formato Hexadecimal.</summary>
		public readonly string ATR;

		/// <summary>Numero Unico Identificador de la Tarjeta. Bytes</summary>
		public readonly byte[] bUID;
		/// <summary>Numero Unico Identificador de la Tarjeta. Hexadecimal</summary>
		public readonly string UID;
		/// <summary>Numero Unico Identificador de la Tarjeta. Entero(long)</summary>
		public readonly long nUID;

		/// <summary>Informacion basica de la Tarjeta</summary>
		public readonly string CardInfo;
		public readonly TagType CardType;

		public void LoadKey(KeyTypes KeyType, byte[] KeyData)
		{
			reader.LoadKey(handle, proto, KeyType, KeyData);
		}
		public void Authenticate(KeyTypes KeyType, byte Sector)
		{
			reader.Authenticate(handle, proto, KeyType, Sector);
		}

		public byte[] Read(byte Page)
		{
			return reader.Read(handle, proto, Page);
		}
		public void Write(byte Page, byte[] Data)
		{
			reader.Write(handle, proto, Page, Data);
		}

		public abstract byte[] ReadAll();
		public abstract void WriteAll(byte[] Data);

		public abstract void NDEFFormat();

		/// <summary>Obtine informacion basica de la Tarjeta.</summary>
		public string GetCardInfo()
		{
			string checkError = string.Empty;
			string retorno = "".PadLeft(256, ' ');

			byte[] _Command = new byte[] { 0xFF, 0xCC, 0x00, 0x00, 0x01, 0x11 }; //"FF CC 00 00 01 11"
			byte[] _ret = reader.Transmit(handle, proto, _Command);
			if (_ret.Length > 0) //01 00 01 90 00
			{
				StringBuilder info = new StringBuilder();

				if (_ret.Length == 3)
				{
					switch (_ret[0])
					{
						case 0: info.Append("No Card Present, "); break;
						case 1: info.Append("Card Present, "); break;
					}
					switch (_ret[1])
					{
						case 0: info.Append("Baud Rate 106Kbps in both directions, "); break;
						case 1: info.Append("Baud Rate 106Kbps from PICC to PCD, 212Kbps from PCD to PICC, "); break;
						case 2: info.Append("Baud Rate 106Kbps from PICC to PCD, 424Kbps from PCD to PICC, "); break;
						case 3: info.Append("Baud Rate 106Kbps from PICC to PCD, 848Kbps from PCD to PICC, "); break;
						case 10: info.Append("Baud Rate 212Kbps from PICC to PCD, 106Kbps from PCD to PICC, "); break;
						case 11: info.Append("Baud Rate 212Kbps in both directions, "); break;
						case 12: info.Append("Baud Rate 212Kbps from PICC to PCD, 424Kbps from PCD to PICC, "); break;
						default: info.Append("Baud Rate Unknown, "); break;
					}
					switch (_ret[2])
					{
						case 0: info.Append("Memory Card Type A."); break;
						case 1: info.Append("Memory Card Type B."); break;
						case 10: info.Append("T=CL Card Type A."); break;
						case 11: info.Append("T=CL Card Type B."); break;
						case 20: info.Append("Dual Mode Card Type A."); break;
						case 21: info.Append("Dual Mode Card Type B."); break;
					}
				}

				retorno = info.ToString();
			}
			
			return retorno;
		}


		//TODO: Testing
		public bool WriteInfo(string pMessage)
		{		
			bool retorno = false;
			try
			{
				/* PROBANDO GUARDAR UN MENSAJE EN LA TARJETA  */
				byte[] hello = pMessage.GetBytes();

				reader.Connect();
				reader.LoadKey(handle, proto, KeyTypes.TypeB, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
				reader.Authenticate(handle, proto, KeyTypes.TypeB, 0x02);
				reader.Write(handle, proto, 0x02, hello);

				byte[] bRead = reader.Read(handle, proto, 0x02);
				if (bRead != null && bRead.Length > 0)
				{
					retorno = true;
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
			return retorno;
		}

		public abstract void Lock();

		public void Dispose()
		{
			int retCode = SCW.SCardDisconnect(handle, SCW.SCARD_UNPOWER_CARD);

			if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed diconnection!");
		}
	}



	public static class Extensions
	{
		/// <summary>Convierte un array de bytes en una Cadena Hexadecimal.</summary>
		public static string BytesToHex(this byte[] bytes)
		{
			string hex = String.Empty;
			for (int idx = 0; idx < bytes.Length; idx++)
			{
				hex = hex + " " + string.Format("{0:X2}", bytes[idx]);
			}
			return hex.Trim();
		}

		/// <summary>Convierte una Cadena Hexadecimal en array de bytes.</summary>
		public static byte[] HexToBytes(this string payload)
		{
			payload = payload.Trim().Replace(" ", "");
			if (payload.Length % 2 > 0) return null;

			byte[] HexAsBytes = new byte[payload.Length / 2];
			for (int index = 0; index < HexAsBytes.Length; index++)
			{
				string byteValue = payload.Substring(index * 2, 2);
				HexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			}
			return HexAsBytes;
		}

		/// <summary>Convierte una cadena de texto en array de bytes.</summary>
		public static byte[] GetBytes(this string str)
		{
			byte[] bytes = new byte[str.Length * sizeof(char)];
			System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
			return bytes;
		}
	}

	public enum ReaderBrand
	{
		Unknown,
		ACS, DUALi, Identive
	}

	public enum TagType
	{
		Unknown, Felica212K, Felica424K, Topaz,
		MifareUltralightFamily, MifareDESFire, MifareMini,
		MifareStandard1K, MifareStandard4K, MifarePlus2K, MifarePlus4K,
		ISO14443A, ISO14443A_part1, ISO14443A_part2, ISO14443A_part3,
		ISO14443B, ISO14443B_part1, ISO14443B_part2, ISO14443B_part3,
		ISO15693, ISO15693_part1, ISO15693_part2, ISO15693_part3, ISO15693_part4,
		ContactCard7816_10_IIC, ContactCard7816_10_ExtendedIIC, ContactCard7816_10_2WBP, ContactCard7816_10_3WBP
	}

	public enum KeyTypes { TypeA, TypeB };
}

namespace SmartCardReader.Tags
{
	class Ultralight : NFCTag
	{
		public Ultralight(IntPtr Handle, int Proto, NFCReader Reader, byte[] ATRbytes) : base(Handle, Proto, Reader, ATRbytes) { }

		public override void NDEFFormat()
		{
			byte[] check;

			check = Read(0x02);
			if (check[2] != 0x00 || check[3] != 0x00)
				throw new Exception("Format failure: tag is locked!");

			check = Read(0x03);
			if (check[0] != 0x00 || check[1] != 0x00 || check[2] != 0x00 || check[3] != 0x00)
				throw new Exception("Format failure: tag is formatted!");

			//Write(0x03, new byte[] { 0xE1, 0x10, 0x06, 0x00 }); // CC for Ultralight
			Write(0x03, new byte[] { 0xE1, 0x10, 0x12, 0x00 }); // CC for NTAG203/UltralightC

			Write(0x04, new byte[] { 0x03, 0x00, 0xFE, 0x00 }); // Empty NDEF record plus terminator
		}

		public override byte[] ReadAll()
		{
			const byte chunkSize = 0x04;
			//const byte bytesToRead = 48;
			const byte bytesToRead = 144;

			byte[] rawData = new byte[bytesToRead];
			int pagesToRead = (bytesToRead / chunkSize);
			for (byte i = 0; i < pagesToRead; i++)
			{
				byte pageIndex = (byte)(i + chunkSize);
				byte[] pageContent = Read(pageIndex);
				Array.Copy(pageContent, 0, rawData, i * chunkSize, chunkSize);
			}
			return rawData;
		}

		public override void WriteAll(byte[] Data)
		{
			for (int i = 0; i < (Data.Length / 4); i++)
			{
				byte[] buffer = new byte[4];
				Array.Copy(Data, i * 4, buffer, 0, 4);
				Write((byte)(i + 4), buffer);
			}
		}

		public override void Lock()
		{
			Write(0x02, new byte[] { 0x00, 0x00, 0xFF, 0xFF });
		}
	}
	class Unknown : NFCTag
	{
		public Unknown(IntPtr Handle, int Proto, NFCReader Reader, byte[] ATRbytes) : base(Handle, Proto, Reader, ATRbytes) { }

		public override void NDEFFormat()
		{
			throw new NotSupportedException();
		}

		public override byte[] ReadAll()
		{
			//throw new NotSupportedException();
			const byte chunkSize = 0x04;
			//const byte bytesToRead = 48;
			const byte bytesToRead = 144;

			byte[] rawData = new byte[bytesToRead];
			int pagesToRead = (bytesToRead / chunkSize);
			for (byte i = 0; i < pagesToRead; i++)
			{
				byte pageIndex = (byte)(i + chunkSize);
				byte[] pageContent = Read(pageIndex);
				Array.Copy(pageContent, 0, rawData, i * chunkSize, chunkSize);
			}
			return rawData;
		}

		public override void WriteAll(byte[] Data)
		{
			throw new NotSupportedException();
		}

		public override void Lock()
		{
			throw new NotSupportedException();
		}
	}
}

namespace SmartCardReader.Readers
{
	class ACSReader : NFCReader
	{
		public ACSReader(IntPtr Handle, string ReaderName) : base(Handle, ReaderName) { }


		public override TagType ParseATR(byte[] bATR)
		{
			TagType Type = TagType.Unknown;

			// Parse ATR received
			string RIDVal, sATRStr, lATRStr, tmpVal;
			int indx, indx2;

			// Mifare cards using ISO 14443 Part 3 Supplemental Document
			if (bATR.Length > 14)
			{
				RIDVal = sATRStr = lATRStr = "";

				for (indx = 7; indx <= 11; indx++)
				{
					RIDVal = RIDVal + " " + string.Format("{0:X2}", bATR[indx]);
				}

				for (indx = 0; indx <= 4; indx++)
				{
					tmpVal = bATR[indx].ToString();

					for (indx2 = 1; indx2 <= 4; indx2++)
					{
						tmpVal = Convert.ToString(Convert.ToInt32(tmpVal) / 2);
					}

					if (((indx == '1') & (tmpVal == "8")))
					{
						lATRStr = lATRStr + "8X";
						sATRStr = sATRStr + "8X";
					}
					else
					{
						if (indx == 4)
						{
							lATRStr = lATRStr + " " + string.Format("{0:X2}", bATR[indx]);
						}
						else
						{
							lATRStr = lATRStr + " " + string.Format("{0:X2}", bATR[indx]);
							sATRStr = sATRStr + " " + string.Format("{0:X2}", bATR[indx]);
						}
					}
				}

				if (RIDVal != "A0 00 00 03 06")
				{
					switch (bATR[12])
					{
						case 0:
							Type = TagType.Unknown;
							break;
						case 1:
							Type = TagType.ISO14443A_part1;
							break;
						case 2:
							Type = TagType.ISO14443A_part2;
							break;
						case 3:
							Type = TagType.ISO14443A_part3;
							break;
						case 5:
							Type = TagType.ISO14443B_part1;
							break;
						case 6:
							Type = TagType.ISO14443B_part2;
							break;
						case 7:
							Type = TagType.ISO14443B_part3;
							break;
						case 9:
							Type = TagType.ISO15693_part1;
							break;
						case 10:
							Type = TagType.ISO15693_part2;
							break;
						case 11:
							Type = TagType.ISO15693_part3;
							break;
						case 12:
							Type = TagType.ISO15693_part4;
							break;
						case 13:
							Type = TagType.ContactCard7816_10_IIC;
							break;
						case 14:
							Type = TagType.ContactCard7816_10_ExtendedIIC;
							break;
						case 15:
							Type = TagType.ContactCard7816_10_2WBP;
							break;
						case 16:
							Type = TagType.ContactCard7816_10_3WBP;
							break;
					}
				}

				// Felica and Topaz Cards
				if (bATR[12] == 0x03)
				{
					if (bATR[13] == 0xF0)
					{
						switch (bATR[14])
						{
							case 0x11:
								Type = TagType.Felica212K;
								break;
							case 0x12:
								Type = TagType.Felica424K;
								break;
							case 0x04:
								Type = TagType.Topaz;
								break;
						}
					}
				}

				if (bATR[12] == 0x03)
				{
					if (bATR[13] == 0x00)
					{
						switch (bATR[14])
						{
							case 0x01:
								Type = TagType.MifareStandard1K;
								break;
							case 0x02:
								Type = TagType.MifareStandard4K;
								break;
							case 0x03:
								Type = TagType.MifareUltralightFamily;
								break;
							case 0x26:
								Type = TagType.MifareMini;
								break;
						}
					}
					else
					{
						if (bATR[13] == 0xFF)
						{
							switch (bATR[14])
							{
								case 0x09:
									Type = TagType.MifareMini;
									break;
							}
						}
					}
				}

			}

			//4.2. Mifare DESFire card using ISO 14443 Part 4
			if (bATR.Length == 11)
			{
				RIDVal = "";
				for (indx = 4; indx <= 9; indx++)
				{
					RIDVal = RIDVal + " " + string.Format("{0:X2}", bATR[indx]);
				}

				if (RIDVal == " 06 75 77 81 02 80")
				{
					Type = TagType.MifareDESFire;
				}

			}

			return Type;
		}

		internal override byte[] ParseUID(IntPtr Handle, int Proto)
		{
			return Transmit(Handle, Proto, new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 });
		}

		public override NFCTag Connect()
		{
			IntPtr handle;
			int proto = 0;
			int retCode = SCW.SCardConnect(hContext, Name, SCW.SCARD_SHARE_SHARED,
								  SCW.SCARD_PROTOCOL_T0 | SCW.SCARD_PROTOCOL_T1, out handle, ref proto);

			if (retCode != SCW.SCARD_S_SUCCESS)
				return null;

			byte[] ATR = GetATR(handle, proto);

			return BuildTag(handle, proto, this, ATR);
		}


		public override string Test()
		{
			throw new NotImplementedException();
		}

		public override void LoadKey(IntPtr Handle, int Proto, KeyTypes KeyType, byte[] KeyData)
		{
			if (KeyData.Length != 6)
				throw new NotSupportedException("Keys must be 6 byte long");

			byte KeyT = KeyType == KeyTypes.TypeA ? (byte)0x60 : (byte)0x61;
			byte KeyN = KeyType == KeyTypes.TypeA ? (byte)0x00 : (byte)0x01;

			Transmit(Handle, Proto, new byte[] { 0xFF, 0x82, 0x00, KeyN, 0x06,
				KeyData[0], KeyData[1], KeyData[2], KeyData[3], KeyData[4], KeyData[5]});
			return;
		}

		public override void Authenticate(IntPtr Handle, int Proto, KeyTypes KeyType, byte Sector)
		{
			byte KeyT = KeyType == KeyTypes.TypeA ? (byte)0x60 : (byte)0x61;
			byte KeyN = KeyType == KeyTypes.TypeA ? (byte)0x00 : (byte)0x01;

			//"FF", "82", "00", "60", "06", "FFFFFFFFFFFF");
			//"FF", "86", "00", "00", "05", string.Format("0100{0:00}6000", pBloque));
			byte[] _ret = Transmit(Handle, Proto, new byte[] { 0xFF, 0x86, 0x00, 0x00, 0x05, 0x01, 0x00, Sector, KeyT, KeyN});
			return;
		}

		public override byte[] Read(IntPtr Handle, int Proto, byte Page)
		{
			return Transmit(Handle, Proto, new byte[] { 0xFF, 0xB0, 0x00, Page, 0x04 });
		}

		public override void Write(IntPtr Handle, int Proto, byte Page, byte[] Data)
		{
			if (Data.Length != 4) throw new Exception("Page write must be of 4 bytes");

			byte[] buffer = new byte[] { 0xFF, 0xD6, 0x00, Page, 0x04, 0x00, 0x00, 0x00, 0x00 };
			Array.Copy(Data, 0, buffer, 5, 4);
			Transmit(Handle, Proto, buffer);
		}

		public override byte[] Transmit(IntPtr Handle, int Proto, byte[] CmdBytes)
		{
			SCW.SCARD_IO_REQUEST ioRequest = new SCW.SCARD_IO_REQUEST();
			ioRequest.dwProtocol = Proto;
			ioRequest.cbPciLength = 8;

			int rcvLenght = 32; // Use 260 to handle more intelligent smartcards
			byte[] rcvBytes = new byte[rcvLenght];

			int retCode = SCW.SCardTransmit(Handle,
				ref ioRequest, CmdBytes, CmdBytes.Length,
				ref ioRequest, rcvBytes, out rcvLenght);

			if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed querying tag: " + SCW.GetScardErrMsg(retCode));

			if (!(rcvBytes[rcvLenght - 2] == 0x90 && rcvBytes[rcvLenght - 1] == 0x00))
			{
				if (rcvBytes[rcvLenght - 2] == 0x63 && rcvBytes[rcvLenght - 1] == 0x00)
					throw new Exception("Operation failed!");

				if (rcvBytes[rcvLenght - 2] == 0x6A && rcvBytes[rcvLenght - 1] == 0x81)
					throw new Exception("Operation not supported!");

				throw new Exception("Operation returned: " + rcvBytes[rcvLenght - 2].ToString("X2") + rcvBytes[rcvLenght - 1].ToString("X2"));
			}

			byte[] returnBytes = new byte[rcvLenght - 2];
			Array.Copy(rcvBytes, returnBytes, rcvLenght - 2);
			return returnBytes;
		}
	}
	class DualiReader : NFCReader
	{
		public DualiReader(IntPtr Handle, string ReaderName) : base(Handle, ReaderName) { }


		public override TagType ParseATR(byte[] bATR)
		{
			if (bATR[4] == 0xF0 || bATR[4] == 0x01)
				switch (bATR[bATR.Length - 1])
				{
					case 0x30:
						return TagType.MifareStandard1K;
					case 0x31:
						return TagType.MifareUltralightFamily;
					case 0x32:
						return TagType.MifareStandard4K;
					case 0x33:
						return TagType.MifareMini;
					case 0x34:
						return TagType.MifarePlus2K;
					case 0x35:
						return TagType.MifarePlus4K;
				}
			else if (bATR[4] == 0xFD || bATR[4] == 0x02)
				return TagType.ISO15693_part1;
			else if (bATR[4] == 0xFC || bATR[4] == 0x03)
				return TagType.Felica212K;
			else if (bATR[4] == 0xF1)
				return TagType.Topaz;

			return TagType.Unknown;
		}

		internal override byte[] ParseUID(IntPtr Handle, int Proto)
		{
			byte[] ATR = GetATR(Handle, Proto);
			if (ATR[4] == 0xF0 || ATR[4] == 0x01)
			{
				byte[] returnBytes = new byte[ATR.Length - 6];
				Array.Copy(ATR, 6, returnBytes, 0, ATR.Length - 6);
				return returnBytes;
			}
			else
				return null;
		}

		public override NFCTag Connect()
		{
			IntPtr handle;
			int proto = 0;
			int retCode = SCW.SCardConnect(hContext, Name, SCW.SCARD_SHARE_SHARED,
								  SCW.SCARD_PROTOCOL_T0 | SCW.SCARD_PROTOCOL_T1, out handle, ref proto);

			if (retCode != SCW.SCARD_S_SUCCESS)
				return null;

			byte[] ATR = GetATR(handle, proto);

			return BuildTag(handle, proto, this, ATR);
		}


		public override string Test()
		{
			throw new NotImplementedException();
		}

		public override void LoadKey(IntPtr Handle, int Proto, KeyTypes KeyType, byte[] KeyData)
		{
			if (KeyData.Length != 6)
				throw new NotSupportedException("Keys must be 6 byte long");

			byte KeyT = KeyType == KeyTypes.TypeA ? (byte)0x00 : (byte)0x04;
			byte KeyN = KeyType == KeyTypes.TypeA ? (byte)0x10 : (byte)0x11;

			Transmit(Handle, Proto, new byte[] { 0xFD, 0x2F, KeyT, KeyN, 0x06,
				KeyData[0], KeyData[1], KeyData[2], KeyData[3], KeyData[4], KeyData[5]});
			return;
		}

		public override void Authenticate(IntPtr Handle, int Proto, KeyTypes KeyType, byte Sector)
		{
			throw new NotImplementedException();
		}

		public override byte[] Read(IntPtr Handle, int Proto, byte Page)
		{
			return Transmit(Handle, Proto, new byte[] { 0xFD, 0x35, 0x00, 0xFF, 0x01, Page });
		}

		public override void Write(IntPtr Handle, int Proto, byte Page, byte[] Data)
		{
			if (Data.Length != 4) throw new Exception("Page write must be of 4 bytes");

			byte[] buffer = new byte[] { 0xFD, 0x37, 0x00, 0xFF, 0x05, Page, 0x00, 0x00, 0x00, 0x00 };
			Array.Copy(Data, 0, buffer, 5, 4);
			Transmit(Handle, Proto, buffer);
		}

		public override byte[] Transmit(IntPtr Handle, int Proto, byte[] CmdBytes)
		{
			SCW.SCARD_IO_REQUEST ioRequest = new SCW.SCARD_IO_REQUEST();
			ioRequest.dwProtocol = Proto;
			ioRequest.cbPciLength = 8;

			int rcvLenght = 32; // Use 260 to handle more intelligent smartcards
			byte[] rcvBytes = new byte[rcvLenght];

			int retCode = SCW.SCardTransmit(Handle,
				ref ioRequest, CmdBytes, CmdBytes.Length,
				ref ioRequest, rcvBytes, out rcvLenght);

			if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed querying tag: " + SCW.GetScardErrMsg(retCode));

			if (!(rcvBytes[rcvLenght - 2] == 0x90 && rcvBytes[rcvLenght - 1] == 0x00))
			{
				if (rcvBytes[rcvLenght - 2] == 0x63 && rcvBytes[rcvLenght - 1] == 0x00)
					throw new Exception("Operation failed!");

				if (rcvBytes[rcvLenght - 2] == 0x6A && rcvBytes[rcvLenght - 1] == 0x81)
					throw new Exception("Operation not supported!");

				throw new Exception("Operation returned: " + rcvBytes[rcvLenght - 2].ToString("X2") + rcvBytes[rcvLenght - 1].ToString("X2"));
			}

			byte[] returnBytes = new byte[rcvLenght - 2];
			Array.Copy(rcvBytes, returnBytes, rcvLenght - 2);
			return returnBytes;
		}
	}
	class IdentiveReader : NFCReader
	{
		public IdentiveReader(IntPtr Handle, string ReaderName) : base(Handle, ReaderName) { }


		public override TagType ParseATR(byte[] bATR)
		{
			TagType Type = TagType.Unknown;

			// Parse ATR received
			string RIDVal, sATRStr, lATRStr, tmpVal;
			int indx, indx2;

			// Mifare cards using ISO 14443 Part 3 Supplemental Document
			if (bATR.Length > 14)
			{
				RIDVal = sATRStr = lATRStr = "";

				for (indx = 7; indx <= 11; indx++)
				{
					RIDVal = RIDVal + " " + string.Format("{0:X2}", bATR[indx]);
				}

				for (indx = 0; indx <= 4; indx++)
				{
					tmpVal = bATR[indx].ToString();

					for (indx2 = 1; indx2 <= 4; indx2++)
					{
						tmpVal = Convert.ToString(Convert.ToInt32(tmpVal) / 2);
					}

					if (((indx == '1') & (tmpVal == "8")))
					{
						lATRStr = lATRStr + "8X";
						sATRStr = sATRStr + "8X";
					}
					else
					{
						if (indx == 4)
						{
							lATRStr = lATRStr + " " + string.Format("{0:X2}", bATR[indx]);
						}
						else
						{
							lATRStr = lATRStr + " " + string.Format("{0:X2}", bATR[indx]);
							sATRStr = sATRStr + " " + string.Format("{0:X2}", bATR[indx]);
						}
					}
				}

				if (RIDVal != "A0 00 00 03 06")
				{
					switch (bATR[12])
					{
						case 0:
							Type = TagType.Unknown;
							break;
						case 1:
							Type = TagType.ISO14443A_part1;
							break;
						case 2:
							Type = TagType.ISO14443A_part2;
							break;
						case 3:
							Type = TagType.ISO14443A_part3;
							break;
						case 5:
							Type = TagType.ISO14443B_part1;
							break;
						case 6:
							Type = TagType.ISO14443B_part2;
							break;
						case 7:
							Type = TagType.ISO14443B_part3;
							break;
						case 9:
							Type = TagType.ISO15693_part1;
							break;
						case 10:
							Type = TagType.ISO15693_part2;
							break;
						case 11:
							Type = TagType.ISO15693_part3;
							break;
						case 12:
							Type = TagType.ISO15693_part4;
							break;
						case 13:
							Type = TagType.ContactCard7816_10_IIC;
							break;
						case 14:
							Type = TagType.ContactCard7816_10_ExtendedIIC;
							break;
						case 15:
							Type = TagType.ContactCard7816_10_2WBP;
							break;
						case 16:
							Type = TagType.ContactCard7816_10_3WBP;
							break;
					}
				}

				// Felica and Topaz Cards
				if (bATR[12] == 0x03)
				{
					if (bATR[13] == 0xF0)
					{
						switch (bATR[14])
						{
							case 0x11:
								Type = TagType.Felica212K;
								break;
							case 0x12:
								Type = TagType.Felica424K;
								break;
							case 0x04:
								Type = TagType.Topaz;
								break;
						}
					}
				}

				if (bATR[12] == 0x03)
				{
					if (bATR[13] == 0x00)
					{
						switch (bATR[14])
						{
							case 0x01:
								Type = TagType.MifareStandard1K;
								break;
							case 0x02:
								Type = TagType.MifareStandard4K;
								break;
							case 0x03:
								Type = TagType.MifareUltralightFamily;
								break;
							case 0x26:
								Type = TagType.MifareMini;
								break;
						}
					}
					else
					{
						if (bATR[13] == 0xFF)
						{
							switch (bATR[14])
							{
								case 0x09:
									Type = TagType.MifareMini;
									break;
							}
						}
					}
				}

			}

			//4.2. Mifare DESFire card using ISO 14443 Part 4
			if (bATR.Length == 11)
			{
				RIDVal = "";
				for (indx = 4; indx <= 9; indx++)
				{
					RIDVal = RIDVal + " " + string.Format("{0:X2}", bATR[indx]);
				}

				if (RIDVal == " 06 75 77 81 02 80")
				{
					Type = TagType.MifareDESFire;
				}

			}

			return Type;
		}

		internal override byte[] ParseUID(IntPtr Handle, int Proto)
		{
			return Transmit(Handle, Proto, new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 });
		}

		public override NFCTag Connect()
		{
			IntPtr handle;
			int proto = 0;
			int retCode = SCW.SCardConnect(hContext, Name, SCW.SCARD_SHARE_SHARED,
								  SCW.SCARD_PROTOCOL_T0 | SCW.SCARD_PROTOCOL_T1, out handle, ref proto);

			if (retCode != SCW.SCARD_S_SUCCESS)
				return null;

			byte[] ATR = GetATR(handle, proto);

			return BuildTag(handle, proto, this, ATR);
		}


		public override string Test()
		{
			throw new NotImplementedException();
		}

		public override void LoadKey(IntPtr Handle, int Proto, KeyTypes KeyType, byte[] KeyData)
		{
			if (KeyData.Length != 6)
				throw new NotSupportedException("Keys must be 6 byte long");

			byte KeyT = KeyType == KeyTypes.TypeA ? (byte)0x60 : (byte)0x61;

			//Comando que Carga la Clave en la Tarjeta: (STORAGE_CARD_CMDS_LOAD_KEYS)
			//									 "FF", "82", "00", "60", "06", "FF FF FF FF FF FF");
			Transmit(Handle, Proto, new byte[] { 0xFF, 0x82, 0x00, KeyT, 0x06, KeyData[0], KeyData[1], KeyData[2], KeyData[3], KeyData[4], KeyData[5]});
			return;
		}

		public override void Authenticate(IntPtr Handle, int Proto, KeyTypes KeyType, byte Sector)
		{
			try
			{
				byte[] _ret = new byte[255];
				byte KeyT = KeyType == KeyTypes.TypeA ? (byte)0x60 : (byte)0x61;

				//Comando que Realiza la Autenticacion: (STORAGE_CARD_CMDS_AUTHENTICATE)
				//											"FF", "86", "00", "00", "05",  01,   00,  {0:00},  60,   00" 
				_ret = Transmit(Handle, Proto, new byte[] { 0xFF, 0x86, 0x00, 0x00, 0x05, 0x01, 0x00, Sector, KeyT, 0x01 }); //60 -> KeyT

			}
			catch (Exception ex)
			{
				throw ex;
			}			
			return;
		}

		public override byte[] Read(IntPtr Handle, int Proto, byte Page)
		{
			return Transmit(Handle, Proto, new byte[] { 0xFF, 0xB0, 0x00, Page, 0x04 });
		}

		public override void Write(IntPtr Handle, int Proto, byte Page, byte[] Data)
		{
			if (Data.Length != 4) throw new Exception("Page write must be of 4 bytes");

			byte[] buffer = new byte[] { 0xFF, 0xD6, 0x00, Page, 0x04, 0x00, 0x00, 0x00, 0x00 };
			Array.Copy(Data, 0, buffer, 5, 4);
			Transmit(Handle, Proto, buffer);
		}


		public override byte[] Transmit(IntPtr Handle, int Proto, byte[] CmdBytes)
		{
			SCW.SCARD_IO_REQUEST ioRequest = new SCW.SCARD_IO_REQUEST();
			ioRequest.dwProtocol = Proto;
			ioRequest.cbPciLength = 8;

			int rcvLenght = 32; // Use 260 to handle more intelligent smartcards
			byte[] rcvBytes = new byte[rcvLenght];

			int retCode = SCW.SCardTransmit(Handle,
				ref ioRequest, CmdBytes, CmdBytes.Length,
				ref ioRequest, rcvBytes, out rcvLenght);

			if (retCode != SCW.SCARD_S_SUCCESS)
				throw new Exception("Failed querying tag: " + SCW.GetScardErrMsg(retCode));

			byte[] returnBytes = new byte[2];
			if (!(rcvBytes[rcvLenght - 2] == 0x90 && rcvBytes[rcvLenght - 1] == 0x00))
			{
				string _ErrCode = string.Format("{0} {1}", rcvBytes[rcvLenght - 2].ToString("X2"), rcvBytes[rcvLenght - 1].ToString("X2"));
				throw new Exception(SCW.errorStatusWord(_ErrCode));
				returnBytes[0] = rcvBytes[rcvLenght - 2];
				returnBytes[1] = rcvBytes[rcvLenght - 1];
			}
			else
			{
				returnBytes = new byte[rcvLenght - 2];
				Array.Copy(rcvBytes, returnBytes, rcvLenght - 2);
			}		
			
			return returnBytes;
		}
	}
}
