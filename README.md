# DynamicMock
Dynamically mock an interface in C#
-------

UPDATE: the current version supports method overloads and properties!

Writing unit tests can be fun, but you'll soon come across objects that are writing to the disk or the DB, etc. The usual solution is to abstract that behaviour with an interface (which can be a pain by itself), but then you have to create mock implementations of that interface, for every test. It leads to a lot of code duplication very quickly.
 
There are lots of solutions out there (certainly for C#), and they are all fairly magical. They use a *substantial* amount of code internally, but you only have to do things like:

	IMyInterface mockInterface = Mock.Create<IMyInterface>();

Boom, no implementation needed (to start with)! :) Example libraries: https://hibernatingrhinos.com/oss/rhino-mocks, https://github.com/ekonbenefits/impromptu-interface, etc.
 
For my project, I didn't want to take a dependency to these libraries unless I had to, so I set out to create something similar in C#. My solution gets you 90% there with a lot less code. You simply inherit from the DynamicMock class and your interface, and you just write a very simple (almost copy-pasteable) line for each method: '(ReturnType)DynamicInvoke(params);'. Example:
 
	class Mock : DynamicMock, MyInterface
	{
		public void doSomething()
		{
			DynamicInvoke();
		}
 	
		public string getValueWithParams(string param1, int param2)
		{
			return (string)DynamicInvoke(param1, param2);
		}
	}
 
Then, in your test code, you do something like:
 
	MyInterfaceMock interfaceMock = new MyInterfaceMock();
 
	interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.doSomething),
            new Action(() => { Trace.WriteLine("doSomething was called!"); }));
 
	interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.getValueWithParams),
		new Func<string, int, string>((string param1, int param2) =>
		{
			Trace.WriteLine($"{nameof(MyInterface.getValueWithParams)} called with {param1}, {param2}.");
			return "works!";
		}));
 
	// The above lines simply store the delegates. The DynamicMock base class will find the matching delegate
	// to invoke when the time comes. Overloads, properties and generics are supported.
	// You don't have to provide an implementation for every method of the interface, only the ones that you
	// know your test will touch.
	// The idea is that the implementations can pretend to have succeeded writing to the DB (etc), or they can
	// return a string being the contents of a fake file, etc. They should also assert that they were called
	// with the expected parameters, etc.
 
	// Now you can proceed to test some complex system that will use the mock
	var complexSystem = new ComplexSystem(interfaceMock);
	complexSystem.DoComplexOperation();
 
	// No exceptions were thrown if we got here, which is good, but we should probably assert a few more things,
	// certainly with respect to the 'ComplexSystem', which is the main thing we are testing anyway. We could
	// also check that the expected methods of the interface were called the correct number of times, etc.

See DynamicMock.cs for the the implementation of the DynamicMock class that you can copy to your project, as well as
a reasonably complete set of tests.
