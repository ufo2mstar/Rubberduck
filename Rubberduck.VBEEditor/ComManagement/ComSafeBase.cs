﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;

#if DEBUG
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
#endif

namespace Rubberduck.VBEditor.ComManagement
{
    public abstract class ComSafeBase : IComSafe
    {
        public abstract void Add(ISafeComWrapper comWrapper);

        public abstract bool TryRemove(ISafeComWrapper comWrapper);

        //We do not use GetHashCode because subclasses of SafeComWrapper<T> overwrite this method 
        //and we need to distinguish between individual instances.
        protected int GetComWrapperObjectHashCode(ISafeComWrapper comWrapper)
        {
            return RuntimeHelpers.GetHashCode(comWrapper);
        }

        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
#if TRACE_COM_SAFE
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (_streamLock)
            {
                try
                {
                    if (_traceStream == null)
                    {
                        return;
                    }

                    _traceStream.Close();
                    if (string.IsNullOrWhiteSpace(_directory))
                    {
                        File.Delete(_traceFilePath);
                    }
                    else
                    {
                        File.Move(_traceFilePath,
                            Path.Combine(_directory,
                                Path.GetFileNameWithoutExtension(_traceFilePath) + " final.csv"));
                    }
                }
                finally
                {
                    _traceStream?.Dispose();
                    _traceStream = null;
                }
            }
#endif
        }

#if TRACE_COM_SAFE
        private struct TraceData
        {
            internal int HashCode { get; set; }
            internal string IUnknownAddress { get; set; }
            internal IEnumerable<string> StackTrace { get; set; }
        }
        private StreamWriter _traceStream;
        private string _traceFilePath;
        private string _directory;
        private readonly object _streamLock = new object();
        
        /// <summary>
        /// The first few stack frames come from the ComSafe and thus are not
        /// particularly interesting. Typically, we want to look at the frames
        /// outside the ComSafe. 
        /// </summary>
        private const int StackTraceNumberOfElementsToSkipOnRemoval = 6;
        private const int StackTrackNumberOfElementsToSkipOnAddUpdate = 8;
        private const int StackTraceDepth = 10;

        /// <inheritdoc cref="IComSafe.Serialize"/>
        public void Serialize(string targetDirectory)
        {
            lock (_streamLock)
            {
                _directory = targetDirectory;
                var serializeTime = DateTime.UtcNow;
                using (var stream = File.AppendText(Path.Combine(_directory,
                    $"COM Safe Content Snapshot {serializeTime:yyyyMMddhhmmss}.csv")))
                {
                    stream.WriteLine(
                        $"Ordinal\tKey\tCOM Wrapper Type\tWrapping Null?\tIUnknown Pointer Address");
                    var i = 0;
                    foreach (var kvp in GetWrappers())
                    {
                        var line = kvp.Value != null
                            ? $"{i++}\t{kvp.Key}\t\"{kvp.Value.GetType().FullName}\"\t\"{kvp.Value.IsWrappingNullReference}\"\t\"{(kvp.Value.IsWrappingNullReference ? "null" : GetPtrAddress(kvp.Value.Target))}\""
                            : $"{i++}\t{kvp.Key}\t\"null\"\t\"null\"\t\"null\"";
                        stream.WriteLine(line);
                    }
                }

                if (_traceStream == null)
                {
                    return;
                }

                _traceStream.Flush();
                File.Copy(_traceFilePath, Path.Combine(_directory, $"COM Safe Stack Trace {serializeTime:yyyyMMddhhmmss}.csv"));
            }
        }

        protected void TraceAdd(ISafeComWrapper comWrapper)
        {
            Trace("Add", comWrapper, StackTrackNumberOfElementsToSkipOnAddUpdate);
        }

        protected void TraceUpdate(ISafeComWrapper comWrapper)
        {
            Trace("Update", comWrapper, StackTrackNumberOfElementsToSkipOnAddUpdate);
        }

        protected void TraceRemove(ISafeComWrapper comWrapper, bool wasRemoved)
        {
            var activity = wasRemoved ? "Removed" : "Not removed";
            Trace(activity, comWrapper, StackTraceNumberOfElementsToSkipOnRemoval);
        }

        private readonly object _idLock = new object();
        private int _id;
        private void Trace(string activity, ISafeComWrapper comWrapper, int framesToSkip)
        {
            lock (_streamLock)
            {
                if (_disposed)
                {
                    return;
                }

                if (_traceStream == null)
                {
                    var directory = Path.GetTempPath();
                    _traceFilePath = Path.Combine(directory,
                        $"COM Safe Stack Trace {DateTime.UtcNow:yyyyMMddhhmmss}.{GetHashCode()}.csv");
                    _traceStream = File.AppendText(_traceFilePath);
                    _traceStream.WriteLine(
                        $"Ordinal\tTimestamp\tActivity\tKey\tIUnknown Pointer Address\t{FrameHeaders()}");
                }

                int id;
                lock (_idLock)
                {
                    id = _id++;
                }

                var traceData = new TraceData
                {
                    HashCode = GetComWrapperObjectHashCode(comWrapper),
                    IUnknownAddress = comWrapper.IsWrappingNullReference ? "null" : GetPtrAddress(comWrapper.Target),
                    StackTrace = GetStackTrace(StackTraceDepth, framesToSkip)
                };

                var line =
                    $"{id}\t{DateTime.UtcNow}\t\"{activity}\"\t{traceData.HashCode}\t{traceData.IUnknownAddress}\t\"{string.Join("\"\t\"", traceData.StackTrace)}\"";
                _traceStream.WriteLine(line);
            }
        }

        private static string FrameHeaders()
        {
            var headers = new System.Text.StringBuilder();
            for(var i = 1; i <= StackTraceDepth; i++)
            {
                headers.Append($"Frame {i}\t");
            }

            return headers.ToString();
        }

        protected abstract IDictionary<int, ISafeComWrapper> GetWrappers();

        private static IEnumerable<string> GetStackTrace(int frames, int framesToSkip)
        {
            var list = new List<string>();
            var trace = new StackTrace();
            if (trace.FrameCount < (frames + framesToSkip))
            {
                frames = trace.FrameCount;
            }
            else
            {
                frames += framesToSkip;
            }

            framesToSkip -= 1;
            frames -= 1;

            for (var i = framesToSkip; i < frames; i++)
            {
                var frame = trace.GetFrame(i);
                var type = frame.GetMethod().DeclaringType;
                
                var typeName = type?.FullName ?? string.Empty;
                var methodName = frame.GetMethod().Name;

                var qualifiedName = $"{typeName}{(typeName.Length > 0 ? "::" : string.Empty)}{methodName}";
                list.Add(qualifiedName);
            }

            return list;
        }

        protected static string GetPtrAddress(object target)
        {
            if (target == null)
            {
                return IntPtr.Zero.ToString();
            }

            if (!Marshal.IsComObject(target))
            {
                return "Not a COM object";
            }

            var pointer = IntPtr.Zero;
            try
            {
                pointer = Marshal.GetIUnknownForObject(target);
            }
            finally
            {
                if (pointer != IntPtr.Zero)
                {
                    Marshal.Release(pointer);
                }
            }

            return pointer.ToString();
        }
#endif
    }
}