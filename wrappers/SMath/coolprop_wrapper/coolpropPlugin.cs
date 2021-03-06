﻿using System.Collections.Generic;
using System.Reflection;
using SMath.Manager;

namespace coolprop_wrapper
{
  public class coolpropPlugin : SMath.Math.IPluginLowLevelEvaluation
  {
    AssemblyInfo[] asseblyInfos = new [] {
      new AssemblyInfo("SMath Studio", new System.Version(0, 97), new System.Guid("a37cba83-b69c-4c71-9992-55ff666763bd"))
    };
    TermInfo[] termInfos = new TermInfo[] {};
    List<IFunction> functions = new List<IFunction>();

    static string AssemblyDirectory {
      get
      {
        var filepath = new System.Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
        return System.IO.Path.GetDirectoryName(filepath);
      }
    }
    static string LogFile {
      get { return System.IO.Path.Combine(AssemblyDirectory, "log.txt"); }
    }

    public static void LogInfo(string Category, string Text, params object[] args)
    {
//#if DEBUG
      var method = new System.Diagnostics.StackFrame(1).GetMethod();
      System.IO.File.AppendAllText(
        LogFile,
        string.Format(
          "{0} {1} {2} [{3}.{4}] {5}{6}",
          System.DateTime.Now.ToShortDateString(),
          System.DateTime.Now.ToLongTimeString(),
          Category,
          method.DeclaringType.Name,
          method.Name.Substring(method.Name.LastIndexOf('.')+1),
          string.Format(Text, args),
          System.Environment.NewLine),
        System.Text.Encoding.UTF8);
//#endif
    }

    public static SMath.Math.Numeric.TNumber GetNumberParam(Term[] arg, ref SMath.Math.Store context)
    {
//      var arg1 = SMath.Math.Decision.Preprocessing(arg, ref context);
//      return SMath.Math.Numeric.Expression.Calculate(arg1, context).obj as SMath.Math.Numeric.TDouble;
      return SMath.Math.Decision.NumericCalculation(arg, ref context);
    }

    public static string GetStringParam(Term[] arg, ref SMath.Math.Store context)
    {
      var dbl = GetNumberParam(arg, ref context).obj as SMath.Math.Numeric.TDouble;
      if (!dbl.isText)
        throw new SMath.Manager.EvaluationException(Errors.ArgumentMustBeString);
      return dbl.ToString().Trim('"');
    }

    public static Term[] MakeDoubleResult(double result, SMath.Math.Symbolic.MItem unit)
    {
      var d = new SMath.Math.Numeric.TDouble(result);
      d.Units = unit;
      return d.ToTerms();
    }

    public static Term[] MakeStringResult(string result)
    {
      return new SMath.Math.Numeric.TDouble("\"" + result + "\"").ToTerms();
    }

    TermInfo[] IPluginHandleEvaluation.TermsHandled { get { return termInfos; } }

    void System.IDisposable.Dispose()
    {
      try {
        if (System.IO.File.Exists(LogFile))
          System.IO.File.Delete(LogFile);
      }
      catch (System.Exception) {}
    }

    AssemblyInfo[] IPlugin.Dependences { get { return asseblyInfos; } }

    void IPlugin.Initialize()
    {
      var info = new List<TermInfo>();

      try {
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes()) {

          if (!(type.IsClass && typeof(IFunction).IsAssignableFrom(type)))
            continue;

          var arguments = type.GetField("Arguments", BindingFlags.GetField | BindingFlags.Static | BindingFlags.Public);

          if (arguments == null)
            continue;

          var args = arguments.GetValue(null) as int[];

          foreach (var arg in args) {
            functions.Add((IFunction)System.Activator.CreateInstance(type, new object[] {arg}));
          }
        }

        foreach (var func in functions) {
          var item = func.GetTermInfo(GlobalParams.CurLang3Letter);
          info.Add(item);
          LogInfo("[INFO ]", "{0}({1}) - {2}", item.Text, item.ChildCount, item.Description);
        }
      }
      catch (System.Exception ex) {
        LogInfo("[ERROR]", "{0}", ex.Message);
      }

      termInfos = info.ToArray();
      LogInfo("[INFO ]", "Successfully. {0} functions loaded.", termInfos.Length);
    }

    bool SMath.Math.IPluginLowLevelEvaluation.ExpressionEvaluation(Term root, Term[][] args, ref SMath.Math.Store context, ref Term[] result)
    {
      if (root.Type != TermType.Function)
        return false;

      foreach (var func in functions) {
        if (!func.Info.Equals(root))
          continue;

        try {
          return func.ExpressionEvaluation(root, args,ref context, ref result);
        }
        catch (System.Exception ex) {
          LogInfo("[ERROR]", "{0}({1}) {2}", root.Text, root.ChildCount, ex.Message);
          throw;
        }
      }

      return false;
    }
  }
}
