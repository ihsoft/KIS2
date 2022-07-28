// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using KSPDev.LogUtils;
using UniLinq;
using UnityEngine;

namespace KSPDev.ProcessingUtils {

/// <summary>
/// The utility class that allows measuring the performance of the methods. Including those that are called frequently.
/// </summary>
/// <remarks>
/// <p>
/// To profile a single call, create a profiler instance right before the method call. Then, report the stats
/// immediately on the method exit.
/// </p>
/// <code><![CDATA[
/// var profiler = new Profiler();
/// profiler(MyMethod, arg1, arg2);  // MyMethod(arg1, arg2);
/// profiler.ReportsStats();
/// ]]></code>
/// <p>
/// To profile multiple calls or calls made in multiple frames, create a profiler instance statically. Then, report the
/// stats from the frame update method.
/// </p>
/// <code><![CDATA[
/// class MyClass : MonoBehaviour {
///   static Profiler _profiler = new(1.0f);  // Update every second if not empty.
///
///   // This method dumps the stats.
///   void Update() {
///     _profiler.ReportStats();
///   }
///
///   // Here we measure the calls.
///   void MyMethodThatNeedsPorfiling() {
///     _profiler(MyMethod, arg1, arg2);
///     if (_profiler(MyFunction, arg1, arg2, arg3) == "blah!") {
///       // ...do stuff...
///     }
///   }
/// }
/// ]]></code>
/// <p>The same profiler can be used to profile multiple method calls, so it's OK to instantiate it as static.</p>
/// </remarks>
public class Profiler {
  readonly float _statsUpdatePeriod; 
  readonly Stopwatch _watch = new();
  readonly Dictionary<string, List<long>> _capturedTicks = new();
  float _lastReportTimestamp;

  #region API methods
  /// <summary>Create a profiler</summary>
  /// <param name="statsUpdatePeriod">
  /// The duration in seconds that specifies how frequently the stats should be dumped.
  /// </param>
  /// <seealso cref="ReportStats"/>
  public Profiler(float statsUpdatePeriod = 0) {
    this._statsUpdatePeriod = statsUpdatePeriod;
  }

  /// <summary>Records performance counters for the method call.</summary>
  /// <param name="methodName">
  /// An arbitrary string to associate with the captured counters. It will be used to group the stats. Multiple calls
  /// for the same name results in accumulating the counters.
  /// </param>
  /// <param name="action">The action to call.</param>
  public void Profile(string methodName, Action action) {
    _watch.Start();
    action.Invoke();
    _watch.Stop();
    RecordTicks(methodName);
  }

  /// <summary>Records performance counters for the function call.</summary>
  /// <param name="methodName">
  /// An arbitrary string to associate with the captured counters. It will be used to group the stats. Multiple calls
  /// for the same name results in accumulating the counters.
  /// </param>
  /// <param name="func">The function to call.</param>
  /// <typeparam name="Res">the type of the returned value.</typeparam>
  /// <returns>The result of type <typeparamref name="Res"/> from the function being profiled.</returns>
  public Res Profile<Res>(string methodName, Func<Res> func) {
    _watch.Start();
    var res = func.Invoke();
    _watch.Stop();
    RecordTicks(methodName);
    return res;
  }

  /// <summary>Records performance counters for the function call.</summary>
  /// <param name="func">The function to call.</param>
  /// <typeparam name="Res">the type of the returned value.</typeparam>
  /// <returns>The result of type <typeparamref name="Res"/> from the function being profiled.</returns>
  public Res Profile<Res>(Func<Res> func) {
    return Profile(func.Method.Name, func.Invoke);
  }

  /// <summary>Records performance counters for the function call.</summary>
  /// <param name="func">The function to call.</param>
  /// <param name="p1">A parameter of the function.</param>
  /// <typeparam name="Res">the type of the returned value.</typeparam>
  /// <typeparam name="T1">the type of <paramref name="p1"/>.</typeparam>
  /// <returns>The result of type <typeparamref name="Res"/> from the function being profiled.</returns>
  public Res Profile<T1, Res>(Func<T1, Res> func, T1 p1) {
    return Profile(func.Method.Name, () => func.Invoke(p1));
  }

  /// <summary>Records performance counters for the function call.</summary>
  /// <param name="func">The function to call.</param>
  /// <param name="p1">A parameter of the function.</param>
  /// <param name="p2">A parameter of the function.</param>
  /// <typeparam name="Res">the type of the returned value.</typeparam>
  /// <typeparam name="T1">the type of <paramref name="p1"/>.</typeparam>
  /// <typeparam name="T2">the type of <paramref name="p2"/>.</typeparam>
  /// <returns>The result of type <typeparamref name="Res"/> from the function being profiled.</returns>
  public Res Profile<T1, T2, Res>(Func<T1, T2, Res> func, T1 p1, T2 p2) {
    return Profile(func.Method.Name, () => func.Invoke(p1, p2));
  }

  /// <summary>Records performance counters for the function call.</summary>
  /// <param name="func">The function to call.</param>
  /// <param name="p1">A parameter of the function.</param>
  /// <param name="p2">A parameter of the function.</param>
  /// <param name="p3">A parameter of the function.</param>
  /// <typeparam name="Res">the type of the returned value.</typeparam>
  /// <typeparam name="T1">the type of <paramref name="p1"/>.</typeparam>
  /// <typeparam name="T2">the type of <paramref name="p2"/>.</typeparam>
  /// <typeparam name="T3">the type of <paramref name="p3"/>.</typeparam>
  /// <returns>The result of type <typeparamref name="Res"/> from the function being profiled.</returns>
  public Res Profile<T1, T2, T3, Res>(Func<T1, T2, T3, Res> func, T1 p1, T2 p2, T3 p3) {
    return Profile(func.Method.Name, () => func.Invoke(p1, p2, p3));
  }

  /// <summary>Records performance counters for the function call.</summary>
  /// <param name="func">The function to call.</param>
  /// <param name="p1">A parameter of the function.</param>
  /// <param name="p2">A parameter of the function.</param>
  /// <param name="p3">A parameter of the function.</param>
  /// <param name="p4">A parameter of the function.</param>
  /// <typeparam name="Res">the type of the returned value.</typeparam>
  /// <typeparam name="T1">the type of <paramref name="p1"/>.</typeparam>
  /// <typeparam name="T2">the type of <paramref name="p2"/>.</typeparam>
  /// <typeparam name="T3">the type of <paramref name="p3"/>.</typeparam>
  /// <typeparam name="T4">the type of <paramref name="p4"/>.</typeparam>
  /// <returns>The result of type <typeparamref name="Res"/> from the function being profiled.</returns>
  public Res Profile<T1, T2, T3, T4, Res>(Func<T1, T2, T3, T4, Res> func, T1 p1, T2 p2, T3 p3, T4 p4) {
    return Profile(func.Method.Name, () => func.Invoke(p1, p2, p3, p4));
  }

  /// <summary>Aggregates the captured counters and prints the result into the logs.</summary>
  /// <remarks>
  /// <p>
  /// Collects all the counters and has them printed per a method name. This method resets all the stats collected this
  /// far. If at the moment of the call there were no counters collected, then no output is produced.
  /// </p>
  /// <p>
  /// It's OK to call this method at a high frequency. It only spits out the output if the
  /// <c>statsUpdatePeriod</c> has expired since the last data output.
  /// </p>
  /// </remarks>
  /// <param name="asWarning">If specified, then the logs are made as WARNING.</param>
  /// <seealso cref="Profiler"/>
  public void ReportStats(bool asWarning = false) {
    if (_lastReportTimestamp + _statsUpdatePeriod > Time.time) {
      return;
    }
    _lastReportTimestamp = Time.time;
    if (_capturedTicks.Count == 0) {
      return;
    }
    var builder = new StringBuilder("Execution stats:\n");//FIXME add tag 
    var keys = _capturedTicks.Keys.ToList();
    keys.Sort();
    foreach (var name in keys) {
      var ticks = _capturedTicks[name];
      long minDuration = long.MaxValue;
      long maxDuration = 0;
      long totalDuration = 0;
      for (var i = ticks.Count - 1; i >= 0; i--) {
        var duration = (long) (1000000.0 * ticks[i] / Stopwatch.Frequency);
        minDuration = Math.Min(minDuration, duration);
        maxDuration = Math.Max(maxDuration, duration);
        totalDuration += duration;
      }
      builder.AppendLine(string.Format("- method {0} stats: calls={1}, min={2}mics, max={3}mics, avg={4}mics",
                             name, ticks.Count, minDuration, maxDuration, totalDuration / ticks.Count));
    }
    _capturedTicks.Clear();
    if (asWarning) {
      DebugEx.Warning(builder.ToString());
    } else {
      DebugEx.Info(builder.ToString());
    }
  }
  #endregion

  #region Local utility methods
  /// <summary>Records the counters from the stopwatch, and resets it.</summary>
  void RecordTicks(string methodName) {
    if (_capturedTicks.TryGetValue(methodName, out var list)) {
      list.Add(_watch.ElapsedTicks);
    } else {
      _capturedTicks.Add(methodName, new List<long> { _watch.ElapsedTicks });
    }
    _watch.Reset();
  }
  #endregion
}
}
