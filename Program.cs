//Copyright 2015 Jon Wolfe/Anibit Technology
// https://anibit.com
//You are hereby granted the right to use or abuse this software 
//in any way that you see fit.

//This code is related to a blog post at: http://www.bytecruft.com/2015/01/stupid-c-tricks-clean-room-code.html

//This code is edited for brevity, and may not reflect general best practices for application development.
//Use at your own risk.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//This class implements the mechanics of the server side.
public static class RemoteHost
{
    //The actual remoting calls coming in from the client take place on a worker thread.
    //This event is so that the main server thread can wait until a worker 
    //thread signal that the server should quit
    public static EventWaitHandle WaitHandle
    {
        get;
        private set;
    }

    //This is the main thread function of the server
    //It sets up remoting and just waits for a Event to be signaled, or for the parent process to exit.
    public static void StartUp(int parentID)
    {
        WaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

        RemotingConfiguration.Configure("server.config", false);

        bool quit = false;
        do
        {
            if (WaitHandle.WaitOne(1000))
            {
                //give time for remoting to complete servicing the call.
                Thread.Sleep(1000);
                quit = true;
            }
            else
            {
                if (parentID != 0)
                {
                    try
                    {
                        var parentProc = Process.GetProcessById(parentID);
                        if (parentProc.HasExited)
                        {
                            quit = true;
                        }
                    }
                    catch
                    {
                        quit = true;
                    }
                }
            }
        } while (!quit);



    }

}


public class Launcher : MarshalByRefObject
{
    //This is the function that is actually called on the server-side
    //The parameters and return value are automatically marshaled by the CLR
    public int Launch(string command, string parameters, string workingDir)
    {
        var proc = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                CreateNoWindow = true,
                Arguments = parameters,
                FileName = command,
                WorkingDirectory = workingDir
            }
        };

        proc.Start();

        proc.WaitForExit();

        int result = proc.ExitCode;
        return result;
    }

    public void Quit()
    {
        RemoteHost.WaitHandle.Set();
    }
}


class Program
{
    static void Main(string[] args)
    {
        if (args != null && args.Length > 0 && args.Any(a => a == "doremote"))
        {
            //then we are running as the child process. 
            int parentID;
            if (!int.TryParse(args[0], out parentID))
            {
                parentID = 0;
            }
            RemoteHost.StartUp(parentID);
        }
        else
        {
            if (args == null || args.Length == 0 || args.All(a => a != "skipremote"))
            {
                LaunchOtherInstance();
            }
            //Now HERE, we launch into our main application!
            RunMainSoftware();
        }

    }


    /// <summary>
    /// Launches another instance of this application, with command line options that cause it to become a remoting server
    /// that accepts RPC calls to launch the Application. The server loads minimal dlls, so to avoid conflicts with dlls and dynamic 
    /// symbols in this processes's environment.
    /// </summary>
    static void LaunchOtherInstance()
    {
        string command = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string args = Process.GetCurrentProcess().Id.ToString() + " doremote ";

        var proc = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = args,
                FileName = command,
            }
        };

        proc.Start();

    }

    private static void RunMainSoftware()
    {
        RemotingConfiguration.Configure("CleanRoomEnvironment.exe.config", false);
        //this is just a proxy for the real object living in the server instance. The CLR handles all the magic.
        var launcher = new Launcher();
        Debug.Assert(RemotingServices.IsTransparentProxy(launcher)); //sanity check

        //our client is setup, now let's do the main logic
        Console.WriteLine("Hello world, I'm going to launch notepad from another process, hold on...");
        launcher.Launch("notepad.exe", "", ".");
        //the above call won't return until the server returns. since the server at this point is waiting
        //for Notepad to exit, we wont get here until Notepad exits. 
        Console.WriteLine("Ok, I'm done!");

        launcher.Quit();
    }

}
