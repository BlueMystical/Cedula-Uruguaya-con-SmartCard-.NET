using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SmartcardLibrary
{
	// https://centroderecursos.agesic.gub.uy/web/seguridad/wiki/-/wiki/Main/Gu%C3%ADa+de+uso+de+CI+electr%C3%B3nica+a+trav%C3%A9s+de+APDU 

	public class UserId
	{

		private String docNumber;
		private String firstName;
		private String lastName1;
		private String lastName2;

		private DateTime birthDateTime;
		private DateTime dateOdIssuance;
		private DateTime dateOfExpiry;

		private String nationality;
		private String placeOfBirth;
		private String pictureB64;
		private String observations;

		private String mrz;

		public UserId(String docNumber, String firstName, String lastName1, String lastName2, DateTime birthDateTime, DateTime dateOdIssuance, DateTime dateOfExpiry, String nationality, String placeOfBirth, String pictureB64, String observations, String mrz)
		{
			this.docNumber = docNumber;
			this.firstName = firstName;
			this.lastName1 = lastName1;
			this.lastName2 = lastName2;
			this.birthDateTime = birthDateTime;
			this.dateOdIssuance = dateOdIssuance;
			this.dateOfExpiry = dateOfExpiry;
			this.nationality = nationality;
			this.placeOfBirth = placeOfBirth;
			this.pictureB64 = pictureB64;
			this.observations = observations;
			this.mrz = mrz;
		}

		public String getDocNumber()
		{
			return this.docNumber;
		}

		public void setDocNumber(String docNumber)
		{
			this.docNumber = docNumber;
		}

		public String getFirstName()
		{
			return this.firstName;
		}

		public void setFirstName(String firstName)
		{
			this.firstName = firstName;
		}

		public String getLastName1()
		{
			return this.lastName1;
		}

		public void setLastName1(String lastName1)
		{
			this.lastName1 = lastName1;
		}

		public String getLastName2()
		{
			return this.lastName2;
		}

		public void setLastName2(String lastName2)
		{
			this.lastName2 = lastName2;
		}

		public DateTime getBirthDateTime()
		{
			return this.birthDateTime;
		}

		public void setBirthDateTime(DateTime birthDateTime)
		{
			this.birthDateTime = birthDateTime;
		}

		public DateTime getDateTimeOdIssuance()
		{
			return this.dateOdIssuance;
		}

		public void setDateTimeOdIssuance(DateTime dateOdIssuance)
		{
			this.dateOdIssuance = dateOdIssuance;
		}

		public DateTime getDateTimeOfExpiry()
		{
			return this.dateOfExpiry;
		}

		public void setDateTimeOfExpiry(DateTime dateOfExpiry)
		{
			this.dateOfExpiry = dateOfExpiry;
		}

		public String getNationality()
		{
			return this.nationality;
		}

		public void setNationality(String nationality)
		{
			this.nationality = nationality;
		}

		public String getPlaceOfBirth()
		{
			return this.placeOfBirth;
		}

		public void setPlaceOfBirth(String placeOfBirth)
		{
			this.placeOfBirth = placeOfBirth;
		}

		public String getPictureB64()
		{
			return this.pictureB64;
		}

		public void setPictureB64(String pictureB64)
		{
			this.pictureB64 = pictureB64;
		}

		public String getObservations()
		{
			return this.observations;
		}

		public void setObservations(String observations)
		{
			this.observations = observations;
		}

		public String getMrz()
		{
			return this.mrz;
		}

		public void setMrz(String mrz)
		{
			this.mrz = mrz;
		}

		public UserId(byte[] buffer, int offset, int length)
		{
			//FABRIZIO: Esta Firma es la semantica que generalmente usan las tarjetas
			//para estas operaciones, aunque no tiene por que ser la que usemos nosotros...


			//TODO build a neat TLV parser
		}

	}

	public class FCITemplate
	{
		//Current FCITemplate data does not support security attributes or access
		//mode bytes

		//TODO solve the EF DF distinction with inheritance
		private bool isDF;
		private bool isEF;
		private String fileName; //only for DF
		private int fileSize; //only for EF
		private byte fdb; //only for EF
		private int fileId;
		private byte lifeCycleStatusByte; //only for EF
										  //TODO 

		public bool isIsDF()
		{
			return this.isDF;
		}

		public void setIsDF(bool isDF)
		{
			this.isDF = isDF;
		}

		public bool isIsEF()
		{
			return this.isEF;
		}

		public void setIsEF(bool isEF)
		{
			this.isEF = isEF;
		}

		public String getFileName()
		{
			return this.fileName;
		}

		public void setFileName(String fileName)
		{
			this.fileName = fileName;
		}

		public int getFileSize()
		{
			return this.fileSize;
		}

		public void setFileSize(int fileSize)
		{
			this.fileSize = fileSize;
		}

		public byte getFdb()
		{
			return this.fdb;
		}

		public void setFdb(byte fdb)
		{
			this.fdb = fdb;
		}

		public int getFileId()
		{
			return this.fileId;
		}

		public void setFileId(int fileId)
		{
			this.fileId = fileId;
		}

		public byte getLifeCycleStatusByte()
		{
			return this.lifeCycleStatusByte;
		}

		public void setLifeCycleStatusByte(byte lifeCycleStatusByte)
		{
			this.lifeCycleStatusByte = lifeCycleStatusByte;
		}


		public FCITemplate()
		{
		}

		public FCITemplate(bool isDF, bool isEF, String fileName, short fileSize, byte fdb, short fileId, byte lifeCycleStatusByte)
		{
			this.isDF = isDF;
			this.isEF = isEF;
			this.fileName = fileName;
			this.fileSize = fileSize;
			this.fdb = fdb;
			this.fileId = fileId;
			this.lifeCycleStatusByte = lifeCycleStatusByte;
		}

		public void buildFromBuffer(byte[] buffer, int offset, int length)
		{
			//FABRIZIO: Esta Firma es la semantica que generalmente usan las tarjetas
			//para estas operaciones, aunque no tiene por que ser la que usemos nosotros...

			//TODO build a neat TLV parser
			if (buffer[offset] != 0x6F)
			{
				throw new Exception("Bad/Unknown FCI Template");
			}
			//TODO support DFs
			switch (buffer[offset + 2])
			{
				case (byte)0x83:
					//DF
					this.isEF = false;

					this.isDF = true;
					//TODO implement a function to convert
					this.fileId = (0x00ff & buffer[offset + 4]) * 256 + (0x00ff & buffer[offset + 5]);

					//DF name Data is last on FCI TLV
					//DF name TLV offset is offset of security attributes length plus the actual length
					//DF name Length position is DF Name TLV offset + 1
					//DF name starts at name TLV offset + 2
					int securityAttributesLength = 0x00ff & buffer[offset + 7];

					int nameTlvOffset = offset + 8 + securityAttributesLength;

					int nameLength = 0x00ff & buffer[nameTlvOffset + 1];

					//TODO: ByteBuffer name = ByteBuffer.wrap(Arrays.copyOfRange(buffer, nameTlvOffset + 2, nameTlvOffset + 2 + nameLength + 1));

					System.IO.MemoryStream name = new System.IO.MemoryStream(buffer, nameTlvOffset + 2, nameTlvOffset + 2 + nameLength + 1);
					byte[] input = name.ToArray();

					//TODO: this.fileName = Utils.asciiDecoder.decode(name).toString();

					break;
				case (byte)0x81:
					//EF

					this.isEF = true;

					this.isDF = false;

					this.fileSize = (0x00ff & buffer[offset + 4]) * 256 + (0x00ff & buffer[offset + 5]);

					this.fdb = buffer[offset + 8];

					this.fileId = (0x00ff & buffer[offset + 11]) * 256 + (0x00ff & buffer[offset + 12]);

					this.lifeCycleStatusByte = buffer[15];
					break;
				default:
					//Unknown FCI
					throw new Exception("Bad/Unknown FCI Template");
			}
		}
	}

	public static class Utils
	{
		public static String subBytes(String a, int beginIndex, int endIndex)
		{
			return a.Substring(beginIndex * 2, endIndex * 2 + 2);
		}

		public static byte[] intToByteArray(int a)
		{
			byte[] b;
			if (a >= 256)
			{
				b = new byte[2];
				b[0] = (byte)(a / 256);
				b[1] = (byte)(a % 256);
			}
			else
			{
				b = new byte[1];
				b[0] = (byte)a;
			}

			return b;
		}

		public static byte[] convertlength(int a)
		{
			if (a >= 256)
			{
				byte[] b = new byte[3];
				byte[] b2 = intToByteArray(a);
				b[0] = (byte)0x00;
				b[1] = b2[0];
				b[2] = b2[1];
				return b;
			}
			else
			{
				return intToByteArray(a);
			}
		}

		public static String byteArrayToHex(byte[] a)
		{
			StringBuilder sb = new StringBuilder(a.Length * 2);
			foreach (byte b in a)
			{
				sb.Append(String.Format("%02X", b));
			}
			return sb.ToString();
		}

		public static String formatHexaString(String hexaString)
		{
			String outHexaString = "";
			hexaString = hexaString.ToUpper();
			int count = 0;//Le agrego espacios a huevo al hexa
			for (int i = 0; i <= hexaString.Length - 1; i++)
			{
				count++;
				outHexaString += hexaString[i];
				if (count % 2 == 0)
				{
					outHexaString += " ";
				}
			}
			return outHexaString;
		}

		public static int Character_Digit(char value, int radix)
		{
			if ((radix <= 0) || (radix > 36))
				return -1; // Or throw exception

			if (radix <= 10)
				if (value >= '0' && value < '0' + radix)
					return value - '0';
				else
					return -1;
			else if (value >= '0' && value <= '9')
				return value - '0';
			else if (value >= 'a' && value < 'a' + radix - 10)
				return value - 'a' + 10;
			else if (value >= 'A' && value < 'A' + radix - 10)
				return value - 'A' + 10;

			return -1;
		}

		public static byte[] hexStringToByteArray(String s)
		{
			int len = s.Length;
			byte[] data = new byte[len / 2];
			for (int i = 0; i < len; i += 2)
			{
				data[i / 2] = (byte)((Character_Digit(s[i], 16) << 4) + Character_Digit(s[i + 1], 16));
			}
			return data;
		}

		public static String readBinary(CardChannel channel, int fileSize)
		{

			// Construyo el Read Binary, lo que cambia en cada read son P1 y P2
			// porque van variando los offset para ir leyendo el binario hasta llegar al tamaño total
			// en cada read leo FF
			String CLASS = "00";
			String INSTRUCTION = "B0";
			String dataIN = "";
			String PARAM1;
			String PARAM2;

			int FF_int = Int32.Parse("FF", System.Globalization.NumberStyles.HexNumber);

			int cantBytes = 0;
			int dataOUTLength = 0; //le

			String binaryHexString = "";

			while (cantBytes < fileSize)
			{

				// Calculo el LE
				// Si la cantidad de Bytes que me quedan por obtener es mayor a
				// FF entonces me traigo FF. Sino me traigo los Bytes que me quedan.
				if (cantBytes + FF_int <= fileSize)
				{
					dataOUTLength = FF_int;
				}
				else
				{
					dataOUTLength = fileSize - cantBytes;
				}

				// Param1 y param2 comienzan en 00 00, voy incrementando FF bytes hasta leer el total del binario.
				String PARAM1_PARAM2 = Utils.byteArrayToHex(Utils.intToByteArray(cantBytes));

				//uso solo p2 porque la cantidad de bytes que voy leyendo es menor a FF
				if (cantBytes <= 255)
				{
					PARAM1 = "00";
					PARAM2 = PARAM1_PARAM2.Substring(0, 2);
				}
				else
				{
					PARAM1 = PARAM1_PARAM2.Substring(0, 2);
					PARAM2 = PARAM1_PARAM2.Substring(2, 4);
				}
				byte CLASSbyte = Utils.hexStringToByteArray(CLASS)[0];
				byte INSbyte = Utils.hexStringToByteArray(INSTRUCTION)[0];
				byte P1byte = Utils.hexStringToByteArray(PARAM1)[0];
				byte P2byte = Utils.hexStringToByteArray(PARAM2)[0];

				//TODO: ResponseAPDU r = Utils.sendCommand(channel, CLASSbyte, INSbyte, P1byte, P2byte, Utils.hexStringToByteArray(dataIN), dataOUTLength);

				//binaryHexString += Utils.byteArrayToHex(r.getData());

				//if (r.getSW1() == (int)0x90 && r.getSW2() == (int)0x00)
				//{

				//	cantBytes += dataOUTLength;

				//}
				//else
				//{
				//	// Fallo algun read binary
				//	return "";
				//}

			}
			return binaryHexString;
		}

		//	public static String[] sacarDatos(CardChannel channel)
		//	{

		//	String[] resultado = new String[5];
		//	readCertificate(channel);
		//	CertificateFactory cf = CertificateFactory.getInstance("X.509");
		//	InputStream b64eIDCertificate = new ByteArrayInputStream(Utils.hexStringToByteArray(certificate_HEX_DER_encoded));
		//	X509Certificate eIDCertificate = (X509Certificate)cf.generateCertificate(b64eIDCertificate);
		//	String certSerialNumber = Utils.formatHexaString(eIDCertificate.getSerialNumber().toString(16));
		//	resultado[0]= certSerialNumber;
		//       resultado[1]=eIDCertificate.getIssuerDN()+"";
		//       resultado[2]=eIDCertificate.getNotBefore()+"";
		//       resultado[3]=eIDCertificate.getNotAfter()+"";
		//       resultado[4]=eIDCertificate.getSubjectDN()+"";
		//       return resultado;
		//   }
		//public String[] sacarFoto(CardChannel channel) throws CertificateException, Exception {
		//       String[] resultado = new String[1];
		//	readCertificate(channel);
		//	FCITemplate fcit7004 = selectFile(channel, "7004");
		//	resultado[0]= readBinary(channel, fcit7004.getFileSize());
		//		return resultado;
		//}

		// Certificado extraido del eID
		private static String certificate_HEX_DER_encoded = "";

		public static bool readCertificate(CardChannel channel)
		{
			FCITemplate fcit = selectFile(channel, "B001");
			certificate_HEX_DER_encoded = readBinary(channel, fcit.getFileSize());

			return true;
		}

		public static FCITemplate selectFile(CardChannel channel, String fileID)
		{
			String CLASS = "00";
			String INSTRUCTION = "A4";
			String PARAM1 = "00";
			String PARAM2 = "00";

			String dataIN = fileID;

			byte CLASSbyte = Utils.hexStringToByteArray(CLASS)[0];
			byte INSbyte = Utils.hexStringToByteArray(INSTRUCTION)[0];
			byte P1byte = Utils.hexStringToByteArray(PARAM1)[0];
			byte P2byte = Utils.hexStringToByteArray(PARAM2)[0];

			//ResponseAPDU r = Utils.sendCommand(channel, CLASSbyte, INSbyte, P1byte, P2byte, Utils.hexStringToByteArray(dataIN), 0);

			//// Si la lectura del archivo es exitosa debo construir el fci template
			//if (r.getSW1() == (int)0x90 && r.getSW2() == (int)0x00)
			//{

			//	FCITemplate fcit = new FCITemplate();
			//	fcit.buildFromBuffer(r.getData(), 0, r.getData().length);
			//	return fcit;

			//}
			//else
			//{

			//	return null;
			//}
			return null;
		}

		public static String[] datosCedula(CardChannel channel)
		{

			String[] resultado = new String[6];
			readCertificate(channel);

			//CertificateFactory cf = CertificateFactory.getInstance("X.509");

			//MemoryStream b64eIDCertificate = new MemoryStream(Utils.hexStringToByteArray(certificate_HEX_DER_encoded));
			//System.Security.Cryptography.X509Certificates.X509Certificate eIDCertificate = (X509Certificate)cf.generateCertificate(b64eIDCertificate);

			//String certSerialNumber = Utils.formatHexaString(eIDCertificate.getSerialNumber().toString(16));

			//resultado[0] = certSerialNumber;
			//resultado[1] = eIDCertificate.getIssuerDN() + "";
			//resultado[2] = eIDCertificate.getNotBefore() + "";
			//resultado[3] = eIDCertificate.getNotAfter() + "";
			//resultado[4] = eIDCertificate.getSubjectDN() + "";

			FCITemplate fcit7004 = selectFile(channel, "7004");
			resultado[5] = readBinary(channel, fcit7004.getFileSize());

			return resultado;
		}

	}

	internal class ResponseAPDU
	{
	}

	public class CardChannel
	{
	}

	/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
	/**
	 *
	 * @author ffaggiani
	 */

	/*

		public class Utils
		{

			//public static CharSet asciiCharset = new CharSet();// Charset.forName("ASCII");
			//public static CharsetEncoder asciiEncoder = asciiCharset.newEncoder();
			//public static CharsetDecoder asciiDecoder = asciiCharset.newDecoder();

			//public static void printBytes(String response, String tag, int beginIndex, int endIndex)
			//{
			//	System.out.println(tag + subBytes(response, beginIndex, endIndex));
			//}

			public static String subBytes(String a, int beginIndex, int endIndex)
			{
				return a.Substring(beginIndex * 2, endIndex * 2 + 2);
			}

			public static byte[] intToByteArray(int a)
			{
				byte[] b;
				if (a >= 256)
				{
					b = new byte[2];
					b[0] = (byte)(a / 256);
					b[1] = (byte)(a % 256);
				}
				else
				{
					b = new byte[1];
					b[0] = (byte)a;
				}

				return b;
			}

			public static byte[] convertlength(int a)
			{
				if (a >= 256)
				{
					byte[] b = new byte[3];
					byte[] b2 = intToByteArray(a);
					b[0] = (byte)0x00;
					b[1] = b2[0];
					b[2] = b2[1];
					return b;
				}
				else
				{
					return intToByteArray(a);
				}
			}

			public static String byteArrayToHex(byte[] a)
			{
				StringBuilder sb = new StringBuilder(a.Length * 2);
				foreach (byte b in a)
				{
					sb.Append(String.Format("%02X", b));
				}
				return sb.ToString();
			}

			public static byte[] hexStringToByteArray(String s)
			{
				int len = s.Length;
				byte[] data = new byte[len / 2];
				for (int i = 0; i < len; i += 2)
				{
					data[i / 2] = (byte)((Character.digit(s.charAt(i), 16) << 4) + Character
							.digit(s.charAt(i + 1), 16));
				}
				return data;
			}

			public static ResponseAPDU sendCommand(CardChannel chan, byte CLASS, byte INS,
					byte P1, byte P2, byte[] data, int le) throws CardException, FileNotFoundException, UnsupportedEncodingException {

			int length = data.length; // largo de la data a mandar
			int i = 0;
			int iteraciones = 0;
			int SW1 = 0, SW2 = 0;
			byte[] command;
			ResponseAPDU r = null;
			LogUtils logUtils = LogUtils.getInstance();

			//si datain vacio
			// mando el comando con LE solo
			if (length == 0) {
				//Si le distinto de 0 lo agrego al final de command           
				command = new byte[5];
				command[0] = CLASS;
				command[1] = INS;
				command[2] = P1;
				command[3] = P2;
				command[4] = intToByteArray(le)[0];
				r = chan.transmit(new CommandAPDU(command));
				SW1 = r.getSW1();
				SW2 = r.getSW2();
				logUtils.logCommand(byteArrayToHex(command),"C");
				logUtils.logCommand(byteArrayToHex(r.getBytes()),"R");
			}
			while (length - i > 0) {
				iteraciones++;
				if (length - i > 0xFF) {
					command = new byte[255 + 6]; //le al final
					command[261] = intToByteArray(le)[0];
					command[0] = (byte) (CLASS | 0x10);
					command[4] = (byte) 0xFF; // mando el maximo de datos que puedo
					System.arraycopy(data, i, command, 5, 0xFF);
				} else {
					if (le > 0 || (le == 0 && length == 0)) {
						command = new byte[length - i + 6];
						command[length - i + 6 - 1] = intToByteArray(le)[0];//le al final
					} else {
						command = new byte[length - i + 5]; //sin  le al final
					}
					command[0] = CLASS;
					command[4] = (byte) (length - i); // mando el maximo de datos
					// que puedo
					System.arraycopy(data, i, command, 5, length - i);
				}
				command[1] = INS;
				command[2] = P1;
				command[3] = P2;

				r = chan.transmit(new CommandAPDU(command));
				SW1 = r.getSW1();
				SW2 = r.getSW2();
				logUtils.logCommand(byteArrayToHex(command),"C");
				logUtils.logCommand(byteArrayToHex(r.getBytes()),"R");

				i += 0xFF;

			}
			return r;
		}

		public static void printDataIN(String datain)
	{

	}

	public static void printCommand(String command)
	{

	}

	public static String PinToAsciiHex(String pin)
	{

		//Return an Hex representation of the input Pin.
		//Each byte in hex represent an ascii digit.
		String pinAscii = "";

		for (int i = 0; i < pin.length(); i++)
		{
			char c = pin.charAt(i);
			String hex = Integer.toHexString((int)c);
			pinAscii = pinAscii.concat(hex);
		}

		//Padding with 00 to complete 12 bytes
		int padding = (24 - pinAscii.length()) / 2;

		for (int j = 0; j < padding; j++)
		{
			pinAscii += "00";
		}

		return pinAscii;
	}

	public static String formatHexaString(String hexaString)
	{
		String outHexaString = "";
		hexaString = hexaString.toUpperCase();
		int count = 0;//Le agrego espacios a huevo al hexa
		for (int i = 0; i <= hexaString.length() - 1; i++)
		{
			count++;
			outHexaString += hexaString.charAt(i);
			if (count % 2 == 0)
			{
				outHexaString += " ";
			}
		}

		return outHexaString;
	}

	public static String bytesToHexFromFile(String filePath)
	{

		//bytesToHex function from file.
		//Situable for minutiate in binary file.
		//Return a single line with hex representation
		File file = new File(filePath);

		FileInputStream fileInputStream = null;

		byte[] bFile = new byte[(int)file.length()];

		String hexfromBinary = "";

		try
		{

			//convert file into array of bytes
			fileInputStream = new FileInputStream(file);
			fileInputStream.read(bFile);
			fileInputStream.close();

			for (int i = 0; i < bFile.length; i++)
			{
				hexfromBinary += ((char)bFile[i]);
			}

		}
		catch (Exception e)
		{

			e.printStackTrace();
		}

		return hexfromBinary;
	}

	public static void sortMinutiae(byte[] minutiae)
	{
		int n = minutiae.length;
		System.out.println("size: " + n);
		int k;
		for (int m = n; m >= 0; m -= 3)
		{
			for (int i = 0; i < m - 3; i += 3)
			{
				k = i + 3;
				if ((minutiae[i + 1] & 0xFF) > (minutiae[k + 1] & 0xFF))
				{
					swapMinutiae(i, k, minutiae);
				}
				else if (minutiae[i + 1] == minutiae[k + 1] && (minutiae[i] & 0xFF) > (minutiae[k] & 0xFF))
				{
					swapMinutiae(i, k, minutiae);
				}
			}
		}
	}

	private static void swapMinutiae(int i, int j, byte[] array)
	{
		byte temp;
		temp = array[i];
		array[i] = array[j];
		array[j] = temp;
		temp = array[i + 1];
		array[i + 1] = array[j + 1];
		array[j + 1] = temp;
		temp = array[i + 2];
		array[i + 2] = array[j + 2];
		array[j + 2] = temp;
	}

	/*
	for input length i, give the BER-LENGTH in Hex String
	*/

	/*public static String berLength(int i)
{

	String berLength = byteArrayToHex(intToByteArray(i));
	if (i > 0xFFFF)
	{
		berLength = "84" + berLength;
	}
	else if (i > 0x7FFF)
	{
		berLength = "83" + berLength;
	}
	else if (i > 0xFF)
	{
		//if length exceeds 0xFF, 82 trails length and two bytes are used
		berLength = "82" + berLength;
	}
	else if (i > 0x7F)
	{
		//if Length exceeds 0x7F (127), 81XX is the length bytes (BER-TLV rules)
		berLength = "81" + berLength;
	}
	return berLength;
}

}
*/

}
