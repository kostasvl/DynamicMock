using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

interface MyInterface
{
    void CallMe();
    string GetValue();

    bool IsSomethingTrue { get; set; }

    // Overloads are supported
    int GetValueWithParams(string param1);
    string GetValueWithParams(string param1, int param2);
    string GetValueWithParams(string param1, string param2);
    int GetValueWithParams(object[] param1);
    float GetValueWithParams(int param1, params object[] parameters);
    T GetValueWithParams<T>(int param1);
    T GetValueWithParams<T>(T param1);

    string this[int i] { get; set; }
}

class DynamicMock
{
    // The Lists are there to store the overloads
    private Dictionary<string, List<Delegate>> mRegularMethodImplementations = new Dictionary<string, List<Delegate>>();
    private Dictionary<string, List<Delegate>> mGenericMethodImplementations = new Dictionary<string, List<Delegate>>();

    // Any subsequent conflicting implementations will be ignored, because the first matching one will be called
    public void AddRegularMethodImplementation(string methodName, Delegate implementation)
    {
        AddMethodImplementation(methodName, implementation, mRegularMethodImplementations);
    }

    // It is easy to force a generic overload to be called, by specifying the generic types as part of the call. We separate the
    // two lists of implementations/Delegates to avoid accidental conflicts with non-generic overloads.
    public void AddGenericMethodImplementation(string methodName, Delegate implementation)
    {
        AddMethodImplementation(methodName, implementation, mGenericMethodImplementations);
    }

    private void AddMethodImplementation(string methodName, Delegate implementation, Dictionary<string, List<Delegate>> methodImplementationsToAddTo)
    {
        List<Delegate> methodOverloads;

        if (!methodImplementationsToAddTo.TryGetValue(methodName, out methodOverloads))
        {
            methodOverloads = new List<Delegate>();
            methodImplementationsToAddTo.Add(methodName, methodOverloads);
        }

        methodOverloads.Add(implementation);
    }

    public enum PropertyType
    {
        Get,
        Set
    }

    public void AddPropertyImplementation(string propertyName, PropertyType propertyType, Delegate implementation)
    {
        if (propertyType == PropertyType.Get)
        {
            AddRegularMethodImplementation("get_" + propertyName, implementation);
        }
        else
        {
            AddRegularMethodImplementation("set_" + propertyName, implementation);
        }
    }

    public void AddIndexerImplementation(PropertyType propertyType, Delegate implementation)
    {
        AddPropertyImplementation("Item", propertyType, implementation);
    }

    protected object DynamicInvoke(params object[] paramsToForward)
    {
        // This works in Release too, at least in my tests...
        var stackFrame = new StackFrame(skipFrames: 1);

        MethodInfo callerMethod = stackFrame.GetMethod() as MethodInfo;

        Dictionary<string, List<Delegate>> methodImplementationsToConsider;
        List<Delegate> methodOverloads;

        if (callerMethod.IsGenericMethod)
        {
            methodImplementationsToConsider = mGenericMethodImplementations;
        }
        else
        {
            methodImplementationsToConsider = mRegularMethodImplementations;
        }

        if (!methodImplementationsToConsider.TryGetValue(callerMethod.Name, out methodOverloads))
        {
            throw new Exception("Unable to find an implementation for this method. Please check the callstack " +
                "and make sure you have provided an implementation for this method/overload!");
        }

        ParameterInfo[] parameters = callerMethod.GetParameters();

        bool matchingMethodImplementationFound = false;

        object objectToReturn = null;

        foreach (Delegate methodImplementation in methodOverloads)
        {
            try
            {
                // Now we'll use Delegate.DynamicInvoke() to try to find a match by actually invoking it. It will throw an exception if
                // it doesn't match! In theory, we could also use...
                //      if (Delegate.CreateDelegate(..., throwOnBindFailure: false) != null)
                // ...but that one throws an exception in at least one of the test cases (a bug?).

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[]))
                {
                    // Because of how easy it is to accidentally expand our "params object[] paramsToForward" into a representation
                    // that will match the wrong delegate, we need to be explicit about the fact that it needs to be an object[]!
                    objectToReturn = methodImplementation.DynamicInvoke(new object[] { paramsToForward });
                }
                else
                {
                    objectToReturn = methodImplementation.DynamicInvoke(paramsToForward);
                }

                matchingMethodImplementationFound = true;
                break;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        if (!matchingMethodImplementationFound)
        {
            throw new Exception($"Unable to find a matching implementation for this method. Please check the callstack and ensure you've provided " +
                $"an implementation for the correct overload (which has {callerMethod.GetParameters().Length} parameters)!");
        }

        return objectToReturn;
    }
}

class MyInterfaceMock : DynamicMock, MyInterface
{
    public bool IsSomethingTrue
    {
        get
        {
            return (bool)DynamicInvoke();
        }
        set
        {
            DynamicInvoke(value);
        }
    }

    public void CallMe() => DynamicInvoke();

    public string GetValue() => (string)DynamicInvoke();

    public int GetValueWithParams(object[] param1) => (int)DynamicInvoke(param1);

    public int GetValueWithParams(string param1) => (int)DynamicInvoke(param1);

    public float GetValueWithParams(int param1, params object[] parameters) => (float)DynamicInvoke(param1, parameters);

    public string GetValueWithParams(string param1, string param2) => (string)DynamicInvoke(param1, param2);

    public string GetValueWithParams(string param1, int param2) => (string)DynamicInvoke(param1, param2);

    public T GetValueWithParams<T>(T param1) => (T)DynamicInvoke(param1);

    public T GetValueWithParams<T>(int param1) => (T)DynamicInvoke(param1);

    public string this[int i]
    {
        get
        {
            return (string)DynamicInvoke(i);
        }
        set
        {
            DynamicInvoke(i, value);
        }
    }
}

class ComplexSystem
{
    private MyInterface mObjectToTest;

    public ComplexSystem(MyInterface objectToTest)
    {
        mObjectToTest = objectToTest;
    }

    public void DoComplexOperation()
    {
        mObjectToTest.CallMe();

        string result = mObjectToTest.GetValue();

        int result2 = mObjectToTest.GetValueWithParams(new object[] { "ObjectArrayItem1", 5555 });

        int result3 = mObjectToTest.GetValueWithParams("Single String");

        float result4 = mObjectToTest.GetValueWithParams(223344, "Params Overload - works :)", "params", "keyword", "is", "fun");

        string result5 = mObjectToTest.GetValueWithParams("Kostas", "Two String Overload - works :)");

        string result6 = mObjectToTest.GetValueWithParams("Hello", 7654);

        string result7 = mObjectToTest.GetValueWithParams<string>("Generic Argument");

        double result8 = mObjectToTest.GetValueWithParams<double>(1234567);

        bool result9 = mObjectToTest.IsSomethingTrue;

        mObjectToTest.IsSomethingTrue = !mObjectToTest.IsSomethingTrue;

        bool result10 = mObjectToTest.IsSomethingTrue;

        mObjectToTest.CallMe();

        string result11 = mObjectToTest[32];
        mObjectToTest[65] = "passing value to indexer";

        Trace.WriteLine(
            $"DONE! Results: {result}, {result2}, {result3}, {result4}, {result5}, {result6}, {result7}, {result8}, {result9}, {result10}, {result11}");
    }
}

public class Program
{
    public static void Main()
    {
        MyInterfaceMock interfaceMock = new MyInterfaceMock();

        uint count = 0;

        interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.CallMe),
            new Action(() => { Trace.WriteLine("CallMe!"); count++; }));

        interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.GetValue),
            new Func<string>(() => { Trace.WriteLine("GetValue!"); return "hi!"; }));

        interfaceMock.AddPropertyImplementation(nameof(MyInterface.IsSomethingTrue), DynamicMock.PropertyType.Get,
            new Func<bool>(() => true));

        interfaceMock.AddPropertyImplementation(nameof(MyInterface.IsSomethingTrue), DynamicMock.PropertyType.Set,
            new Action<bool>((bool value) => Trace.WriteLine($"IsSomethingTrue, value given: {value}")));

        interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.GetValueWithParams),
            new Func<string, int, string>((string param1, int param2) =>
            {
                Trace.WriteLine($"{nameof(MyInterface.GetValueWithParams)} called with {param1}, {param2}.");
                return "works!";
            }));

        interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.GetValueWithParams),
            new Func<string, string, string>((string param1, string param2) =>
            {
                Trace.WriteLine($"{nameof(MyInterface.GetValueWithParams)} called with {param1}, {param2}.");
                return "works too!";
            }));

        interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.GetValueWithParams),
            new Func<string, int>((string param1) =>
            {
                Trace.WriteLine($"{nameof(MyInterface.GetValueWithParams)} called with {param1}.");
                return 35;
            }));

        interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.GetValueWithParams),
            new Func<object[], int>((object[] param1) =>
            {
                Trace.WriteLine($"{nameof(MyInterface.GetValueWithParams)} called with {param1}.");
                return 11111111;
            }));

        interfaceMock.AddRegularMethodImplementation(nameof(MyInterface.GetValueWithParams),
            new Func<int, object[], float>((int param1, object[] paramsToForward) =>
            {
                Trace.WriteLine($"{nameof(MyInterface.GetValueWithParams)} called with {param1}, {paramsToForward}.");
                return 3.14f;
            }));

        interfaceMock.AddGenericMethodImplementation(nameof(MyInterface.GetValueWithParams),
            new Func<string, string>((string param1) =>
            {
                Trace.WriteLine($"{nameof(MyInterface.GetValueWithParams)} called with {param1}.");
                return "works also!";
            }));

        interfaceMock.AddGenericMethodImplementation(nameof(MyInterface.GetValueWithParams),
            new Func<int, double>((int param1) =>
            {
                Trace.WriteLine($"{nameof(MyInterface.GetValueWithParams)} called with {param1}.");
                return 3.14564756477689;
            }));

        // An indexer property gets a backing property called "Item", so AddIndexerImplementation just automates that part,
        // by calling AddPropertyImplementation
        interfaceMock.AddIndexerImplementation(DynamicMock.PropertyType.Get,
            new Func<int, string>((int index) =>
            {
                Trace.WriteLine($"Indexer getter called with index {index}.");
                return index.ToString();
            }));

        interfaceMock.AddIndexerImplementation(DynamicMock.PropertyType.Set,
            new Action<int, string>((int index, string value) =>
            {
                Trace.WriteLine($"Indexer getter called with index {index} and value \"{value}\".");
            }));

        var complexSystem = new ComplexSystem(interfaceMock);

        complexSystem.DoComplexOperation();

        Trace.WriteLine($"{nameof(MyInterface.CallMe)} was called {count} times.");
    }
}
