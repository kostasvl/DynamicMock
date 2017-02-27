# DynamicMock
Dynamically mock an interface in C#
--

Writing unit tests can be fun, but you'll soon come across objects that are writing to the disk or the DB, etc. The usual solution is to abstract that behaviour with an interface (which can be a pain by itself), but then you have to create mock implementations of that interface, for every test. It leads to a lot of code duplication very quickly.
 
There are lots of solutions out there (certainly for C#), and they are all fairly magical. They use a *substantial* amount of code internally, but you only have to do things like: IMyInterface mockInterface = Mock.Create<IMyInterface>. Boom, no implementation needed (to start with)! :) Example libraries: https://hibernatingrhinos.com/oss/rhino-mocks, https://github.com/ekonbenefits/impromptu-interface, etc.
 
For my project, I didn't want to take a dependency to these libraries unless I had to, so I set out to create something similar in C#. My solution gets you 90% there with a lot less code. You simply inherit from the DynamicMock class and your interface, and you just write a very simple (almost copy-pasteable) line for each method: 'DynamicInvoke("methodName");'. Example:
 
---------------------------
class Mock : DynamicMock, MyInterface
{
   public void callMe()
   {
      DynamicInvoke(System.Reflection.MethodBase.GetCurrentMethod().Name);
   }
 
   public string getValueWithParams(string param1, int param2)
   {
      return (string)DynamicInvoke(System.Reflection.MethodBase.GetCurrentMethod().Name, param1, param2);
   }
}
---------------------------
 
Then in your test code, you do something like:
 
---------------------------
Mock interfaceMock = new Mock();
 
interfaceMock.DynamicInterface.doSomething =
   new Action(() => { Console.WriteLine("doSomething was called."); });
 
// The above line dynamically creates a new member called "doSomething", which is of type Action/delegate. It could then be called with 'interfaceMock.DynamicInterface.doSomething()', but it's actually done in a more low-level way internally.
// You don't have to provide an implementation for every method of the interface, only the ones that you know your test will touch.
// The idea is that the implementations can pretend to have succeeded writing to the DB (etc), or they can return a string being the contents of a fake file, etc. They should also assert that they were called with the expected parameters, etc.
// Here's an example with parameters and a return value:
 
interfaceMock.DynamicInterface.getValueWithParams =
   new Func<string, int, string>((string param1, int param2) => { Console.WriteLine($"getValueWithParams! param1: {param1}, param2: {param2}"); return "Success!"; });
 
// Ready to start testing some complex system now :)
var complexSystem = new ComplexSystem(interfaceMock);
 
complexSystem.DoComplexOperation();
 
// No exceptions were thrown if we got here, which is good, but we should probably assert a few more things, certainly with respect to the 'ComplexSystem', which is the main thing we are testing anyway. We could also check that the expected methods of the interface were called the correct number of times, etc.
---------------------------
 
The 'DynamicInterface' is basically a .NET 4 ExpandoObject, exposed as a 'dynamic' variable. When I started, I thought that using these things would be a must, but I think what I ended up with could be done with just objects and reflection. Anyway, see DynamicMock.cs for the final solution.
