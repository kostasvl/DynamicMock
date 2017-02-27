using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;

interface MyInterface
{
    void doSomething();
    string getValue();
	string getValueWithParams(string param1, int param2);
}

class DynamicMock
{
	private ExpandoObject mExpandoObject = new ExpandoObject();
	
    public dynamic DynamicInterface => mExpandoObject;
    
	private Dictionary<string, uint> mMethodCallCounts = new Dictionary<string, uint>();
	
	protected object DynamicInvoke(string methodName, params object[] paramsToForward)
	{
		try
		{
			// I really don't get the reason for this cast. ExpandoObject inherits from this thing!
			var expandoAsDictionary = (IDictionary<string, object>)mExpandoObject;
			
			// These are always expected to be Actions or Funcs
			object invokableObject = expandoAsDictionary[methodName];
			
			MethodInfo invokeMethod = invokableObject.GetType().GetMethod("Invoke");
			
			// Invoke the Invoke method of the Action or Func :)
			object resultObject = invokeMethod.Invoke(invokableObject, paramsToForward);
			
			incrementMethodCallCount(methodName);
			
			return resultObject;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"DynamicInvoke({methodName}) threw an exception: {ex}");
			throw;
		}
	}

	public uint GetMethodCallCount(string methodName)
	{
		uint callCount = 0;
		
		mMethodCallCounts.TryGetValue(methodName, out callCount);
		
		return callCount;
	}
	
	private void incrementMethodCallCount(string methodName)
	{
		uint currentCount;
		
		if (mMethodCallCounts.TryGetValue(methodName, out currentCount))
		{
			mMethodCallCounts[methodName] = currentCount + 1;
		}
		else
		{
			mMethodCallCounts[methodName] = 1;
		}
	}
}

class Mock : DynamicMock, MyInterface
{
    public void doSomething()
    {
		DynamicInvoke(System.Reflection.MethodBase.GetCurrentMethod().Name);
    }
    
    public string getValue()
    {
        return (string)DynamicInvoke(System.Reflection.MethodBase.GetCurrentMethod().Name);
    }
	
	public string getValueWithParams(string param1, int param2)
	{
		return (string)DynamicInvoke(System.Reflection.MethodBase.GetCurrentMethod().Name, param1, param2);
	}
}

class ComplexSystem
{
	private MyInterface mMyInterface;
	
	public ComplexSystem(MyInterface myInterface)
	{
		mMyInterface = myInterface;
	}
	
	public void DoComplexOperation()
	{
		mMyInterface.doSomething();
		
		string result = mMyInterface.getValue();
		
		string result2 = mMyInterface.getValueWithParams("A string parameter", 123456);
		
		mMyInterface.doSomething();
		
		Console.WriteLine($"Done! Results: {result}, {result2}");
	}
}

public class Program
{
	public static void Main()
	{
		Mock interfaceMock = new Mock();

		interfaceMock.DynamicInterface.doSomething = new Action(() =>
			{ Console.WriteLine("doSomething() was called"); });
		
		interfaceMock.DynamicInterface.getValue = new Func<string>(() =>
			{ Console.WriteLine("getValue() was called"); return "hi!"; });
		
		interfaceMock.DynamicInterface.getValueWithParams = new Func<string, int, string>((string param1, int param2) =>
			{ Console.WriteLine($"getValueWithParams() was called, param1: {param1}, param2: {param2}"); return "Success!"; });
		
		var complexSystem = new ComplexSystem(interfaceMock);
		
		complexSystem.DoComplexOperation();
		
		Console.WriteLine($"doSomething() was called {interfaceMock.GetMethodCallCount("doSomething")} times.");
		Console.WriteLine($"getValue() was called {interfaceMock.GetMethodCallCount("getValue")} times.");
		Console.WriteLine($"getValueWithParams() was called {interfaceMock.GetMethodCallCount("getValueWithParams")} times.");
	}
}
