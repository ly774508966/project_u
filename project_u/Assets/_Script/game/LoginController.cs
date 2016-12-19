using UnityEngine;
using System.Collections;

public class LoginController : MonoBehaviour {

	const string kHost = "localhost";
	const ushort kPort = 5000;
	
	void Awake()
	{
	}

	void Start()
	{
		/*
		var memoryStream = new System.IO.MemoryStream();
		var codedStream = new Google.Protobuf.CodedOutputStream(memoryStream);

		var keyExchange = new Proto.ExchangeKey();
		keyExchange.Header = new Proto.Header();
		keyExchange.Header.Version = 1;
		keyExchange.Header.ProtoName = Proto.ExchangeKey.Descriptor.Name;
		keyExchange.Key0 = 10;
		keyExchange.Key1 = 20;
		keyExchange.WriteTo(codedStream);

		codedStream.Flush();
		memoryStream.Seek(0, System.IO.SeekOrigin.Begin);

		var buffer = new byte[512];
		memoryStream.Read(buffer, 0, buffer.Length);
		memoryStream.Close();

		//Msg_Login.Parser.ParseFrom(buffer);
		var protoNameLength = buffer[2+1+1+1];
		var headerSize = 0+2+1+1+protoNameLength;

		var inputStream = new Google.Protobuf.CodedInputStream(buffer, 0, headerSize);
		var header = Proto.Header.Parser.ParseFrom(inputStream);
		var desc = Proto.BasicReflection.Descriptor.FindTypeByName<Google.Protobuf.Reflection.IDescriptor>(header);
		Debug.Assert(Proto.Pike.Descriptor.Name == desc.Name);

		inputStream = new Google.Protobuf.CodedInputStream(buffer);
	*/
 
		
		
		
		


		comext.utils.App.PerformTaskOnMainThread((state) => Debug.Log("EEE"));
	}

	void Login()
	{
	}

	void HandleLoginResult()
	{

	}
		 
	
}
