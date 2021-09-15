// Kerbal Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Diagnostics;
using KSPDev.LogUtils;

// ReSharper disable once CheckNamespace
namespace KSPDev.DebugUtils {

/// <summary>A scope that measures the duration of execution.</summary>
/// <remarks>
/// On the scope exits, this class will log a warning log record that tells how many microseconds the code execution
/// within the scope has took.
/// </remarks>
public class ProfilerScope : IDisposable {
	#region Local fields and properties
	static readonly double MicrosPerTick = 1000.0*1000.0 / Stopwatch.Frequency;
	readonly PartModule _context;
	readonly string _tag;
	readonly Stopwatch _watch;
	#endregion

	/// <summary>Creates a scope that measures the execution duration.</summary>
	/// <param name="context">The module to bind the log records to.</param>
	/// <param name="tag">A tag to add to the log record to identify the code block being measured.</param>
	public ProfilerScope(PartModule context, string tag) {
		_context = context;
		_tag = tag;
		_watch = Stopwatch.StartNew();
	}

	/// <summary>Closes the scope and logs the duration in microseconds.</summary>
	/// <remarks>The measured duration will be as precise as the system can measure.</remarks>
	public void Dispose() {
		_watch.Stop();
		HostedDebugLog.Warning(
				_context, "Profiler: tag={0}, duration={1} us", _tag, Math.Floor(MicrosPerTick * _watch.ElapsedTicks));
	}
}

}  // namespace
