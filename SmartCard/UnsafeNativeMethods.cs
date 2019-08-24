using System;
using System.Runtime.InteropServices;

namespace SmartcardLibrary
{
	internal enum ScopeOption
	{
		None = 0,
		Terminal = 1,
		System = 2
	}

	/// <summary>Clase que implementa las llamadas a Funciones API de Windows requeridas para la comunicacion con el dispositivo lector de tarjetas.
	/// ** ACTUALIZADO PARA EQUIPOS CON SISTEMA OPERATIVO DE 64 BITS ***</summary>
	internal sealed partial class UnsafeNativeMethods
	{
		#region WinScard.DLL Imports

		[DllImport("WinScard.dll")]
		static internal extern int SCardEstablishContext(
			uint dwScope,
			IntPtr notUsed1,
			IntPtr notUsed2,
			out IntPtr phContext);

		[DllImport("WinScard.dll")]
		static internal extern int SCardReleaseContext(IntPtr phContext);

		[DllImport("WINSCARD.DLL", EntryPoint = "SCardListReaders", CharSet = CharSet.Unicode, SetLastError = true)]
		static internal extern uint ListReaders(
			IntPtr context,
			string groups,
			string readers, ref int size);

		[DllImport("WINSCARD.DLL", EntryPoint = "SCardGetStatusChange", CharSet = CharSet.Unicode, SetLastError = true)]
		static internal extern uint GetStatusChange(
			[In(), Out()] IntPtr context,
			[In(), Out()] int timeout,
			[In(), Out()] ReaderState[] states,
			[In(), Out()] int count);

		[DllImport("WinScard.dll", EntryPoint = "SCardListReadersA", CharSet = CharSet.Ansi)]
		static internal extern int SCardListReaders(
			IntPtr hContext,
			byte[] mszGroups,
			byte[] mszReaders,
			ref UInt32 pcchReaders);

		[StructLayout(LayoutKind.Sequential)]
		public struct SCARD_IO_REQUEST
		{
			public UInt32 dwProtocol;
			public UInt32 cbPciLength;
		}

		[DllImport("winscard.dll")]
		static internal extern int SCardTransmit(
			IntPtr hCard,
			ref SCARD_IO_REQUEST pioSendRequest,
			ref byte SendBuff,
			uint SendBuffLen,
			ref SCARD_IO_REQUEST pioRecvRequest,
			byte[] RecvBuff,
			ref uint RecvBuffLen);

		[DllImport("WinScard.dll")]
		static internal extern int SCardConnect(IntPtr hContext,
			string cReaderName,
			uint dwShareMode,
			uint dwPrefProtocol,
			ref IntPtr phCard,
			ref IntPtr ActiveProtocol);

		[DllImport("WinScard.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
		static internal extern int SCardDisconnect(IntPtr hCard, int Disposition);


		[DllImport("WinScard.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
		public static extern int SCardState(IntPtr hCard, ref int State, ref int Protocol, ref byte ATR, ref int ATRLen);

		[DllImport("WinScard.dll", EntryPoint = "SCardStatusA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
		public static extern int SCardStatus(
			IntPtr hCard,
			[MarshalAs(UnmanagedType.VBByRefStr)] ref string szReaderName,
			ref int pcchReaderLen,
			ref int State,
			ref int Protocol, byte[] ATR, ref int ATRLen);

		[DllImport("winscard.dll", SetLastError = true)]
		public static extern Int32 SCardGetAttrib(
			IntPtr hCard,            // Reference value returned from SCardConnect
			UInt32 dwAttrId,         // Identifier for the attribute to get
			byte[] pbAttr,           // Pointer to a buffer that receives the attribute
			ref IntPtr pcbAttrLen    // Length of pbAttr in bytes
		);

		#endregion
	}
}