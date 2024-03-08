#region Apache License
//
// Licensed to the Apache Software Foundation (ASF) under one or more 
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership. 
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with 
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

using log4net.Util;
using log4net.Core;
using System.Threading;

#nullable enable

[assembly: InternalsVisibleTo("log4net.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100297dcac908e28689360399027b0ea4cd852fbb74e1ed95e695a5ba55cbd1d075ec20cdb5fa6fc593d3d571527b20558d6f39e1f4d5cfe0798428c589c311965244b209c38a02aaa8c9da3b72405b6fedeeb4292c3457e9769b74e645c19cb06c2be75fb2d12281a585fbeabf7bd195d6961ba113286fc3e286d7bbd69024ceda")]

namespace log4net.Appender
{
#if CONFIRM_WIN32_FILE_SHAREMODES
  // The following sounds good, and I thought it was the case, but after
  // further testing on Windows I have not been able to confirm it.

  /// On the Windows platform if another process has a write lock on the file 
  /// that is to be deleted, but allows shared read access to the file then the
  /// file can be moved, but cannot be deleted. If the other process also allows 
  /// shared delete access to the file then the file will be deleted once that 
  /// process closes the file. If it is necessary to open the log file or any
  /// of the backup files outside of this appender for either read or 
  /// write access please ensure that read and delete share modes are enabled.
#endif

  /// <summary>
  /// Appender that rolls log files based on size or date or both.
  /// </summary>
  /// <remarks>
  /// <para>
  /// RollingFileAppender can roll log files based on size or date or both
  /// depending on the setting of the <see cref="RollingStyle"/> property.
  /// When set to <see cref="RollingMode.Size"/> the log file will be rolled
  /// once its size exceeds the <see cref="MaximumFileSize"/>.
  /// When set to <see cref="RollingMode.Date"/> the log file will be rolled
  /// once the date boundary specified in the <see cref="DatePattern"/> property
  /// is crossed.
  /// When set to <see cref="RollingMode.Composite"/> the log file will be
  /// rolled once the date boundary specified in the <see cref="DatePattern"/> property
  /// is crossed, but within a date boundary the file will also be rolled
  /// once its size exceeds the <see cref="MaximumFileSize"/>.
  /// When set to <see cref="RollingMode.Once"/> the log file will be rolled when
  /// the appender is configured. This effectively means that the log file can be
  /// rolled once per program execution.
  /// </para>
  /// <para>
  /// A of few additional optional features have been added:
  /// <list type="bullet">
  /// <item>Attach date pattern for current log file <see cref="StaticLogFileName"/></item>
  /// <item>Backup number increments for newer files <see cref="CountDirection"/></item>
  /// <item>Infinite number of backups by file size <see cref="MaxSizeRollBackups"/></item>
  /// </list>
  /// </para>
  /// 
  /// <note>
  /// <para>
  /// For large or infinite numbers of backup files a <see cref="CountDirection"/> 
  /// greater than zero is highly recommended, otherwise all the backup files need
  /// to be renamed each time a new backup is created.
  /// </para>
  /// <para>
  /// When Date/Time based rolling is used setting <see cref="StaticLogFileName"/> 
  /// to <see langword="true"/> will reduce the number of file renamings to few or none.
  /// </para>
  /// </note>
  /// 
  /// <note type="caution">
  /// <para>
  /// Changing <see cref="StaticLogFileName"/> or <see cref="CountDirection"/> without clearing
  /// the log file directory of backup files will cause unexpected and unwanted side effects.  
  /// </para>
  /// </note>
  /// 
  /// <para>
  /// If Date/Time based rolling is enabled this appender will attempt to roll existing files
  /// in the directory without a Date/Time tag based on the last write date of the base log file.
  /// The appender only rolls the log file when a message is logged. If Date/Time based rolling 
  /// is enabled then the appender will not roll the log file at the Date/Time boundary but
  /// at the point when the next message is logged after the boundary has been crossed.
  /// </para>
  /// 
  /// <para>
  /// The <see cref="RollingFileAppender"/> extends the <see cref="FileAppender"/> and
  /// has the same behavior when opening the log file.
  /// The appender will first try to open the file for writing when <see cref="ActivateOptions"/>
  /// is called. This will typically be during configuration.
  /// If the file cannot be opened for writing the appender will attempt
  /// to open the file again each time a message is logged to the appender.
  /// If the file cannot be opened for writing when a message is logged then
  /// the message will be discarded by this appender.
  /// </para>
  /// <para>
  /// When rolling a backup file necessitates deleting an older backup file the
  /// file to be deleted is moved to a temporary name before being deleted.
  /// </para>
  /// 
  /// <note type="caution">
  /// <para>
  /// A maximum number of backup files when rolling on date/time boundaries is not supported.
  /// </para>
  /// </note>
  /// </remarks>
  /// <author>Nicko Cadell</author>
  /// <author>Gert Driesen</author>
  /// <author>Aspi Havewala</author>
  /// <author>Douglas de la Torre</author>
  /// <author>Edward Smit</author>
  public class RollingFileAppender : FileAppender
  {
    #region Public Enums

    /// <summary>
    /// Style of rolling to use
    /// </summary>
    /// <remarks>
    /// <para>
    /// Style of rolling to use
    /// </para>
    /// </remarks>
    public enum RollingMode
    {
      /// <summary>
      /// Roll files once per program execution
      /// </summary>
      /// <remarks>
      /// <para>
      /// Roll files once per program execution.
      /// Well really once each time this appender is
      /// configured.
      /// </para>
      /// <para>
      /// Setting this option also sets <c>AppendToFile</c> to
      /// <c>false</c> on the <c>RollingFileAppender</c>, otherwise
      /// this appender would just be a normal file appender.
      /// </para>
      /// </remarks>
      Once = 0,

      /// <summary>
      /// Roll files based only on the size of the file
      /// </summary>
      Size = 1,

      /// <summary>
      /// Roll files based only on the date
      /// </summary>
      Date = 2,

      /// <summary>
      /// Roll files based on both the size and date of the file
      /// </summary>
      Composite = 3
    }

    #endregion

    #region Protected Enums

    /// <summary>
    /// The code assumes that the following 'time' constants are in a increasing sequence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The code assumes that the following 'time' constants are in a increasing sequence.
    /// </para>
    /// </remarks>
    protected internal enum RollPoint
    {
      /// <summary>
      /// Roll the log not based on the date
      /// </summary>
      InvalidRollPoint = -1,

      /// <summary>
      /// Roll the log for each minute
      /// </summary>
      TopOfMinute = 0,

      /// <summary>
      /// Roll the log for each hour
      /// </summary>
      TopOfHour = 1,

      /// <summary>
      /// Roll the log twice a day (midday and midnight)
      /// </summary>
      HalfDay = 2,

      /// <summary>
      /// Roll the log each day (midnight)
      /// </summary>
      TopOfDay = 3,

      /// <summary>
      /// Roll the log each week
      /// </summary>
      TopOfWeek = 4,

      /// <summary>
      /// Roll the log each month
      /// </summary>
      TopOfMonth = 5
    }

    #endregion Protected Enums

    #region Public Instance Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RollingFileAppender" /> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default constructor.
    /// </para>
    /// </remarks>
    public RollingFileAppender()
    {
    }

    /// <summary>
    /// Cleans up all resources used by this appender.
    /// </summary>
    ~RollingFileAppender()
    {
      if (m_mutexForRolling != null)
      {
        m_mutexForRolling.Dispose();
        m_mutexForRolling = null;
      }
    }

    #endregion Public Instance Constructors

    #region Public Instance Properties

    /// <summary>
    /// Gets or sets the strategy for determining the current date and time. The default
    /// implementation is to use LocalDateTime which internally calls through to DateTime.Now. 
    /// DateTime.UtcNow may be used on frameworks newer than .NET 1.0 by specifying
    /// <see cref="RollingFileAppender.UniversalDateTime"/>.
    /// </summary>
    /// <value>
    /// An implementation of the <see cref="RollingFileAppender.IDateTime"/> interface which returns the current date and time.
    /// </value>
    /// <remarks>
    /// <para>
    /// Gets or sets the <see cref="RollingFileAppender.IDateTime"/> used to return the current date and time.
    /// </para>
    /// <para>
    /// There are two built strategies for determining the current date and time, 
    /// <see cref="RollingFileAppender.LocalDateTime"/>
    /// and <see cref="RollingFileAppender.UniversalDateTime"/>.
    /// </para>
    /// <para>
    /// The default strategy is <see cref="RollingFileAppender.LocalDateTime"/>.
    /// </para>
    /// </remarks>
    public IDateTime DateTimeStrategy { get; set; } = new LocalDateTime();

    /// <summary>
    /// Gets or sets the date pattern to be used for generating file names
    /// when rolling over on date.
    /// </summary>
    /// <value>
    /// The date pattern to be used for generating file names when rolling 
    /// over on date.
    /// </value>
    /// <remarks>
    /// <para>
    /// Takes a string in the same format as expected by 
    /// <see cref="log4net.DateFormatter.SimpleDateFormatter" />.
    /// May be set to null to disable date formatting.
    /// </para>
    /// <para>
    /// This property determines the rollover schedule when rolling over
    /// on date.
    /// </para>
    /// </remarks>
    public string? DatePattern { get; set; } = ".yyyy-MM-dd";

    /// <summary>
    /// Gets or sets the maximum number of backup files that are kept before
    /// the oldest is erased.
    /// </summary>
    /// <value>
    /// The maximum number of backup files that are kept before the oldest is
    /// erased.
    /// </value>
    /// <remarks>
    /// <para>
    /// If set to zero, then there will be no backup files and the log file 
    /// will be truncated when it reaches <see cref="MaxFileSize"/>.  
    /// </para>
    /// <para>
    /// If a negative number is supplied then no deletions will be made.  Note 
    /// that this could result in very slow performance as a large number of 
    /// files are rolled over unless <see cref="CountDirection"/> is used.
    /// </para>
    /// <para>
    /// The maximum applies to <b>each</b> time based group of files and 
    /// <b>not</b> the total.
    /// </para>
    /// </remarks>
    public int MaxSizeRollBackups { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum size that the output file is allowed to reach
    /// before being rolled over to backup files.
    /// </summary>
    /// <value>
    /// The maximum size in bytes that the output file is allowed to reach before being 
    /// rolled over to backup files.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property is equivalent to <see cref="MaximumFileSize"/> except
    /// that it is required for differentiating the setter taking a
    /// <see cref="long"/> argument from the setter taking a <see cref="string"/> 
    /// argument.
    /// </para>
    /// <para>
    /// The default maximum file size is 10MB (10*1024*1024).
    /// </para>
    /// </remarks>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum size that the output file is allowed to reach
    /// before being rolled over to backup files.
    /// </summary>
    /// <value>
    /// The maximum size that the output file is allowed to reach before being 
    /// rolled over to backup files.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property allows you to specify the maximum size with the
    /// suffixes "KB", "MB" or "GB" so that the size is interpreted being 
    /// expressed respectively in kilobytes, megabytes or gigabytes. 
    /// </para>
    /// <para>
    /// For example, the value "10KB" will be interpreted as 10240 bytes.
    /// </para>
    /// <para>
    /// The default maximum file size is 10MB.
    /// </para>
    /// <para>
    /// If you have the option to set the maximum file size programmatically
    /// consider using the <see cref="MaxFileSize"/> property instead as this
    /// allows you to set the size in bytes as a <see cref="Int64"/>.
    /// </para>
    /// </remarks>
    public string MaximumFileSize
    {
      get { return MaxFileSize.ToString(NumberFormatInfo.InvariantInfo); }
      set { MaxFileSize = OptionConverter.ToFileSize(value, MaxFileSize + 1); }
    }

    /// <summary>
    /// Gets or sets the rolling file count direction. 
    /// </summary>
    /// <value>
    /// The rolling file count direction.
    /// </value>
    /// <remarks>
    /// <para>
    /// Indicates if the current file is the lowest numbered file or the
    /// highest numbered file.
    /// </para>
    /// <para>
    /// By default newer files have lower numbers (<see cref="CountDirection" /> &lt; 0),
    /// i.e. log.1 is most recent, log.5 is the 5th backup, etc...
    /// </para>
    /// <para>
    /// <see cref="CountDirection" /> &gt;= 0 does the opposite i.e.
    /// log.1 is the first backup made, log.5 is the 5th backup made, etc.
    /// For infinite backups use <see cref="CountDirection" /> &gt;= 0 to reduce 
    /// rollover costs.
    /// </para>
    /// <para>The default file count direction is -1.</para>
    /// </remarks>
    public int CountDirection { get; set; } = -1;

    /// <summary>
    /// Gets or sets the rolling style.
    /// </summary>
    /// <value>The rolling style.</value>
    /// <remarks>
    /// <para>
    /// The default rolling style is <see cref="RollingMode.Composite" />.
    /// </para>
    /// <para>
    /// When set to <see cref="RollingMode.Once"/> this appender's
    /// <see cref="FileAppender.AppendToFile"/> property is set to <c>false</c>, otherwise
    /// the appender would append to a single file rather than rolling
    /// the file each time it is opened.
    /// </para>
    /// </remarks>
    public RollingMode RollingStyle
    {
      get { return m_rollingStyle; }
      set
      {
        m_rollingStyle = value;
        switch (m_rollingStyle)
        {
          case RollingMode.Once:
            m_rollDate = false;
            m_rollSize = false;

            AppendToFile = false;
            break;

          case RollingMode.Size:
            m_rollDate = false;
            m_rollSize = true;
            break;

          case RollingMode.Date:
            m_rollDate = true;
            m_rollSize = false;
            break;

          case RollingMode.Composite:
            m_rollDate = true;
            m_rollSize = true;
            break;
        }
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to preserve the file name extension when rolling.
    /// </summary>
    /// <value>
    /// <c>true</c> if the file name extension should be preserved.
    /// </value>
    /// <remarks>
    /// <para>
    /// By default file.log is rolled to file.log.yyyy-MM-dd or file.log.curSizeRollBackup.
    /// However, under Windows the new file name will lose any program associations as the
    /// extension is changed. Optionally file.log can be renamed to file.yyyy-MM-dd.log or
    /// file.curSizeRollBackup.log to maintain any program associations.
    /// </para>
    /// </remarks>
    public bool PreserveLogFileNameExtension { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to always log to
    /// the same file.
    /// </summary>
    /// <value>
    /// <c>true</c> if always should be logged to the same file, otherwise <c>false</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// By default file.log is always the current file.  Optionally
    /// file.log.yyyy-mm-dd for current formatted datePattern can by the currently
    /// logging file (or file.log.curSizeRollBackup or even
    /// file.log.yyyy-mm-dd.curSizeRollBackup).
    /// </para>
    /// <para>
    /// This will make time based rollovers with a large number of backups 
    /// much faster as the appender it won't have to rename all the backups!
    /// </para>
    /// </remarks>
    public bool StaticLogFileName { get; set; } = true;

    #endregion Public Instance Properties

    #region Private Static Fields

    /// <summary>
    /// The fully qualified type of the RollingFileAppender class.
    /// </summary>
    /// <remarks>
    /// Used by the internal logger to record the Type of the
    /// log message.
    /// </remarks>
    private static readonly Type declaringType = typeof(RollingFileAppender);

    #endregion Private Static Fields

    #region Override implementation of FileAppender 

    /// <summary>
    /// Sets the quiet writer being used.
    /// </summary>
    /// <remarks>
    /// This method can be overridden by subclasses.
    /// </remarks>
    /// <param name="writer">the writer to set</param>
    protected override void SetQWForFiles(TextWriter writer)
    {
      QuietWriter = new CountingQuietTextWriter(writer, ErrorHandler);
    }

    /// <summary>
    /// Write out a logging event.
    /// </summary>
    /// <param name="loggingEvent">the event to write to file.</param>
    /// <remarks>
    /// <para>
    /// Handles append time behavior for RollingFileAppender.  This checks
    /// if a roll over either by date (checked first) or time (checked second)
    /// is need and then appends to the file last.
    /// </para>
    /// </remarks>
    protected override void Append(LoggingEvent loggingEvent)
    {
      AdjustFileBeforeAppend();
      base.Append(loggingEvent);
    }

    /// <summary>
    /// Write out an array of logging events.
    /// </summary>
    /// <param name="loggingEvents">the events to write to file.</param>
    /// <remarks>
    /// <para>
    /// Handles append time behavior for RollingFileAppender.  This checks
    /// if a roll over either by date (checked first) or time (checked second)
    /// is need and then appends to the file last.
    /// </para>
    /// </remarks>
    protected override void Append(LoggingEvent[] loggingEvents)
    {
      AdjustFileBeforeAppend();
      base.Append(loggingEvents);
    }

    /// <summary>
    /// Performs any required rolling before outputting the next event
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles append time behavior for RollingFileAppender.  This checks
    /// if a roll over either by date (checked first) or time (checked second)
    /// is need and then appends to the file last.
    /// </para>
    /// </remarks>
    protected virtual void AdjustFileBeforeAppend()
    {
      // reuse the file appenders locking model to lock the rolling
      try
      {
        // if rolling should be locked, acquire the lock
        m_mutexForRolling?.WaitOne();
        if (m_rollDate)
        {
          var n = DateTimeStrategy.Now;
          if (n >= m_nextCheck)
          {
            m_now = n;
            m_nextCheck = NextCheckDate(m_now, m_rollPoint);

            RollOverTime(true);
          }
        }

        if (m_rollSize)
        {
          if ((File != null) && ((CountingQuietTextWriter)QuietWriter).Count >= MaxFileSize)
          {
            RollOverSize();
          }
        }
      }
      finally
      {
          // if rolling should be locked, release the lock
          m_mutexForRolling?.ReleaseMutex();
      }
    }

    /// <summary>
    /// Creates and opens the file for logging.  If <see cref="StaticLogFileName"/>
    /// is false then the fully qualified name is determined and used.
    /// </summary>
    /// <param name="fileName">the name of the file to open</param>
    /// <param name="append">true to append to existing file</param>
    /// <remarks>
    /// <para>This method will ensure that the directory structure
    /// for the <paramref name="fileName"/> specified exists.</para>
    /// </remarks>
    protected override void OpenFile(string fileName, bool append)
    {
      lock (this)
      {
        fileName = GetNextOutputFileName(fileName);

        // Calculate the current size of the file
        long currentCount = 0;
        if (append)
        {
          using (SecurityContext?.Impersonate(this))
          {
            if (System.IO.File.Exists(fileName))
            {
              currentCount = (new FileInfo(fileName)).Length;
            }
          }
        }
        else
        {
          if (LogLog.IsErrorEnabled)
          {
            // Internal check that the file is not being overwritten
            // If not Appending to an existing file we should have rolled the file out of the
            // way. Therefore we should not be over-writing an existing file.
            // The only exception is if we are not allowed to roll the existing file away.
            if (MaxSizeRollBackups != 0 && FileExists(fileName))
            {
              LogLog.Error(declaringType, "RollingFileAppender: INTERNAL ERROR. Append is False but OutputFile [" + fileName + "] already exists.");
            }
          }
        }

        if (!StaticLogFileName)
        {
          m_scheduledFilename = fileName;
        }

        // Open the file (call the base class to do it)
        base.OpenFile(fileName, append);

        // Set the file size onto the counting writer
        ((CountingQuietTextWriter)QuietWriter).Count = currentCount;
      }
    }

    /// <summary>
    /// Get the current output file name
    /// </summary>
    /// <param name="fileName">the base file name</param>
    /// <returns>the output file name</returns>
    /// <remarks>
    /// The output file name is based on the base fileName specified.
    /// If <see cref="StaticLogFileName"/> is set then the output 
    /// file name is the same as the base file passed in. Otherwise
    /// the output file depends on the date pattern, on the count
    /// direction or both.
    /// </remarks>
    protected string GetNextOutputFileName(string fileName)
    {
      if (!StaticLogFileName)
      {
        fileName = fileName.Trim();

        if (m_rollDate)
        {
          fileName = CombinePath(fileName, m_now.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo));
        }

        if (CountDirection >= 0)
        {
          fileName = CombinePath(fileName, "." + m_curSizeRollBackups);
        }
      }

      return fileName;
    }

    #endregion

    #region Initialize Options

    /// <summary>
    /// Determines curSizeRollBackups (only within the current roll point)
    /// </summary>
    private void DetermineCurSizeRollBackups()
    {
      m_curSizeRollBackups = 0;

      string? fullPath;
      string? fileName;

      using (SecurityContext?.Impersonate(this))
      {
        fullPath = Path.GetFullPath(m_baseFileName!);
        fileName = Path.GetFileName(fullPath);
      }

      List<string> arrayFiles = GetExistingFiles(fullPath);
      InitializeRollBackups(fileName, arrayFiles);

      LogLog.Debug(declaringType, "curSizeRollBackups starts at [" + m_curSizeRollBackups + "]");
    }

    /// <summary>
    /// Generates a wildcard pattern that can be used to find all files
    /// that are similar to the base file name.
    /// </summary>
    private string GetWildcardPatternForFile(string baseFileName)
    {
      if (PreserveLogFileNameExtension)
      {
        return Path.GetFileNameWithoutExtension(baseFileName) + "*" + Path.GetExtension(baseFileName);
      }
      else
      {
        return baseFileName + '*';
      }
    }

    /// <summary>
    /// Builds a list of filenames for all files matching the base filename plus a file
    /// pattern.
    /// </summary>
    // Internal for unit testing.
    internal List<string> GetExistingFiles(string baseFilePath)
    {
      var alFiles = new List<string>();

      string? directory;
      using (SecurityContext?.Impersonate(this))
      {
        var fullPath = Path.GetFullPath(baseFilePath);

        directory = Path.GetDirectoryName(fullPath);
        if (Directory.Exists(directory))
        {
          var baseFileName = Path.GetFileName(fullPath);

          var files = Directory.GetFiles(directory, GetWildcardPatternForFile(baseFileName));
          for (var i = 0; i < files.Length; i++)
          {
            var curFileName = Path.GetFileName(files[i]);
            if (curFileName.StartsWith(Path.GetFileNameWithoutExtension(baseFileName)))
            {
              alFiles.Add(curFileName);
            }
          }
        }
      }
      LogLog.Debug(declaringType, "Searched for existing files in [" + directory + "]");
      return alFiles;
    }

    /// <summary>
    /// Initiates a roll over if needed for crossing a date boundary since the last run.
    /// </summary>
    private void RollOverIfDateBoundaryCrossing()
    {
      if (StaticLogFileName && m_rollDate)
      {
        if (m_baseFileName is not null && FileExists(m_baseFileName))
        {
          DateTime last;
          using (SecurityContext?.Impersonate(this))
          {
            if (DateTimeStrategy is UniversalDateTime)
            {
              last = System.IO.File.GetLastWriteTimeUtc(m_baseFileName);
            }
            else
            {
              last = System.IO.File.GetLastWriteTime(m_baseFileName);
            }
          }
          LogLog.Debug(declaringType, "[" + last.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo) + "] vs. [" + m_now.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo) + "]");

          if (!(last.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo).Equals(m_now.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo))))
          {
            m_scheduledFilename = CombinePath(m_baseFileName, last.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo));
            LogLog.Debug(declaringType, "Initial roll over to [" + m_scheduledFilename + "]");
            RollOverTime(false);
            LogLog.Debug(declaringType, "curSizeRollBackups after rollOver at [" + m_curSizeRollBackups + "]");
          }
        }
      }
    }

    /// <summary>
    /// Initializes based on existing conditions at time of <see cref="ActivateOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Initializes based on existing conditions at time of <see cref="ActivateOptions"/>.
    /// The following is done
    /// <list type="bullet">
    ///  <item>determine curSizeRollBackups (only within the current roll point)</item>
    ///  <item>initiates a roll over if needed for crossing a date boundary since the last run.</item>
    ///  </list>
    ///  </para>
    /// </remarks>
    protected void ExistingInit()
    {
      DetermineCurSizeRollBackups();
      RollOverIfDateBoundaryCrossing();

      // If file exists and we are not appending then roll it out of the way
      if (AppendToFile)
      {
        return;
      }

      bool fileExists;
      string fileName = GetNextOutputFileName(m_baseFileName!);

      using (SecurityContext?.Impersonate(this))
      {
        fileExists = System.IO.File.Exists(fileName);
      }

      if (!fileExists)
      {
        return;
      }

      if (MaxSizeRollBackups == 0)
      {
        LogLog.Debug(declaringType, "Output file [" + fileName + "] already exists. MaxSizeRollBackups is 0; cannot roll. Overwriting existing file.");
      }
      else
      {
        LogLog.Debug(declaringType, "Output file [" + fileName + "] already exists. Not appending to file. Rolling existing file out of the way.");

        RollOverRenameFiles(fileName);
      }
    }

    /// <summary>
    /// Does the work of bumping the 'current' file counter higher
    /// to the highest count when an incremental file name is seen.
    /// The highest count is either the first file (when count direction
    /// is greater than 0) or the last file (when count direction less than 0).
    /// In either case, we want to know the highest count that is present.
    /// </summary>
    /// <param name="baseFile"></param>
    /// <param name="curFileName"></param>
    private void InitializeFromOneFile(string baseFile, string curFileName)
    {
      curFileName = curFileName.ToLower();
      baseFile = baseFile.ToLower();
      if (curFileName.StartsWith(Path.GetFileNameWithoutExtension(baseFile)) == false)
      {
        // This is not a log file, so ignore
        return;
      }
      if (curFileName.Equals(baseFile))
      {
        // Base log file is not an incremented logfile (.1 or .2, etc.)
        return;
      }

      // Only look for files in the current roll point
      if (m_rollDate && !StaticLogFileName)
      {
        var date = DateTimeStrategy.Now.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo).ToLower();
        var prefix = (PreserveLogFileNameExtension
          ? Path.GetFileNameWithoutExtension(baseFile) + date
          : baseFile + date).ToLower();
        var suffix = PreserveLogFileNameExtension
          ? Path.GetExtension(baseFile).ToLower()
          : "";
        if (!curFileName.StartsWith(prefix) || !curFileName.EndsWith(suffix))
        {
          LogLog.Debug(declaringType, "Ignoring file [" + curFileName + "] because it is from a different date period");
          return;
        }
      }

      try
      {
        // Bump the counter up to the highest count seen so far
        var backup = GetBackUpIndex(curFileName);

        // caution: we might get a false positive when certain
        // date patterns such as yyyyMMdd are used...those are
        // valid number but aren't the kind of back up index
        // we're looking for
        if (backup > m_curSizeRollBackups)
        {
          if (0 == MaxSizeRollBackups)
          {
            // Stay at zero when zero backups are desired
          }
          else if (-1 == MaxSizeRollBackups)
          {
            // Infinite backups, so go as high as the highest value
            m_curSizeRollBackups = backup;
          }
          else
          {
            // Backups limited to a finite number
            if (CountDirection >= 0)
            {
              // Go with the highest file when counting up
              m_curSizeRollBackups = backup;
            }
            else
            {
              // Clip to the limit when counting down
              if (backup <= MaxSizeRollBackups)
              {
                m_curSizeRollBackups = backup;
              }
            }
          }
          LogLog.Debug(declaringType, "File name [" + curFileName + "] moves current count to [" + m_curSizeRollBackups + "]");
        }
      }
      catch (FormatException)
      {
        //this happens when file.log -> file.log.yyyy-MM-dd which is normal
        //when staticLogFileName == false
        LogLog.Debug(declaringType, "Encountered a backup file not ending in .x [" + curFileName + "]");
      }
    }

    /// <summary>
    /// Attempts to extract a number from the end of the file name that indicates
    /// the number of the times the file has been rolled over.
    /// </summary>
    /// <remarks>
    /// Certain date pattern extensions like yyyyMMdd will be parsed as valid backup indexes.
    /// </remarks>
    private int GetBackUpIndex(string curFileName)
    {
      var backUpIndex = -1;
      var fileName = curFileName;

      if (PreserveLogFileNameExtension)
      {
        fileName = Path.GetFileNameWithoutExtension(fileName);
      }

      var index = fileName.LastIndexOf(".", StringComparison.Ordinal);
      if (index > 0)
      {
        // if the "yyyy-MM-dd" component of file.log.yyyy-MM-dd is passed to TryParse
        // it will gracefully fail and return backUpIndex will be 0
        SystemInfo.TryParse(fileName.Substring(index + 1), out backUpIndex);
      }

      return backUpIndex;
    }

    /// <summary>
    /// Takes a list of files and a base file name, and looks for 
    /// 'incremented' versions of the base file.  Bumps the max
    /// count up to the highest count seen.
    /// </summary>
    // Internal for unit testing.
    internal void InitializeRollBackups(string baseFile, List<string> arrayFiles)
    {
      var baseFileLower = baseFile.ToLowerInvariant();

      foreach (string curFileName in arrayFiles)
      {
        InitializeFromOneFile(baseFileLower, curFileName.ToLowerInvariant());
      }
    }

    /// <summary>
    /// Calculates the RollPoint for the datePattern supplied.
    /// </summary>
    /// <param name="datePattern">the date pattern to calculate the check period for</param>
    /// <returns>The RollPoint that is most accurate for the date pattern supplied</returns>
    /// <remarks>
    /// Essentially the date pattern is examined to determine what the
    /// most suitable roll point is. The roll point chosen is the roll point
    /// with the smallest period that can be detected using the date pattern
    /// supplied. i.e. if the date pattern only outputs the year, month, day 
    /// and hour then the smallest roll point that can be detected would be
    /// and hourly roll point as minutes could not be detected.
    /// </remarks>
    // Internal for unit testing.
    internal RollPoint ComputeCheckPeriod(string datePattern)
    {
      // s_date1970 is 1970-01-01 00:00:00 this is UniversalSortableDateTimePattern 
      // (based on ISO 8601) using universal time. This date is used for reference
      // purposes to calculate the resolution of the date pattern.

      // Get string representation of base line date
      string r0 = s_date1970.ToString(datePattern, DateTimeFormatInfo.InvariantInfo);

      // Check each type of rolling mode starting with the smallest increment.
      for (var i = (int)RollPoint.TopOfMinute; i <= (int)RollPoint.TopOfMonth; i++)
      {
        // Get string representation of next pattern
        string r1 = NextCheckDate(s_date1970, (RollPoint)i).ToString(datePattern, DateTimeFormatInfo.InvariantInfo);

        LogLog.Debug(declaringType, "Type = [" + i + "], r0 = [" + r0 + "], r1 = [" + r1 + "]");

        // Check if the string representations are different
        if (!r0.Equals(r1))
        {
          // Found highest precision roll point
          return (RollPoint)i;
        }
      }

      return RollPoint.InvalidRollPoint; // Deliberately head for trouble...
    }

    /// <summary>
    /// Initialize the appender based on the options set
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is part of the <see cref="IOptionHandler"/> delayed object
    /// activation scheme. The <see cref="ActivateOptions"/> method must 
    /// be called on this object after the configuration properties have
    /// been set. Until <see cref="ActivateOptions"/> is called this
    /// object is in an undefined state and must not be used. 
    /// </para>
    /// <para>
    /// If any of the configuration properties are modified then 
    /// <see cref="ActivateOptions"/> must be called again.
    /// </para>
    /// <para>
    /// Sets initial conditions including date/time roll over information, first check,
    /// scheduledFilename, and calls <see cref="ExistingInit"/> to initialize
    /// the current number of backups.
    /// </para>
    /// </remarks>
    public override void ActivateOptions()
    {
      if (m_rollDate && DatePattern != null)
      {
        m_now = DateTimeStrategy.Now;
        m_rollPoint = ComputeCheckPeriod(DatePattern);

        if (m_rollPoint == RollPoint.InvalidRollPoint)
        {
          throw new ArgumentException("Invalid RollPoint, unable to parse [" + DatePattern + "]");
        }

        // next line added as this removes the name check in rollOver
        m_nextCheck = NextCheckDate(m_now, m_rollPoint);
      }
      else
      {
        if (m_rollDate)
        {
          ErrorHandler.Error("Either DatePattern or rollingStyle options are not set for [" + Name + "].");
        }
      }

      SecurityContext ??= SecurityContextProvider.DefaultProvider.CreateSecurityContext(this);

      using (SecurityContext.Impersonate(this))
      {
        // Must convert the FileAppender's m_filePath to an absolute path before we
        // call ExistingInit(). This will be done by the base.ActivateOptions() but
        // we need to duplicate that functionality here first.
        base.File = ConvertToFullPath(base.File!.Trim());

        // Store fully qualified base file name
        m_baseFileName = base.File;
      }

      // initialize the mutex that is used to lock rolling
      m_mutexForRolling = new Mutex(false, m_baseFileName
        .Replace("\\", "_")
        .Replace(":", "_")
        .Replace("/", "_") + "_rolling"
      );

      if (m_rollDate && File != null && m_scheduledFilename == null)
      {
        m_scheduledFilename = CombinePath(File, m_now.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo));
      }

      ExistingInit();

      base.ActivateOptions();
    }

    #endregion

    #region Roll File

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path1"></param>
    /// <param name="path2">.1, .2, .3, etc.</param>
    /// <returns></returns>
    private string CombinePath(string path1, string path2)
    {
      var extension = Path.GetExtension(path1);
      if (PreserveLogFileNameExtension && extension.Length > 0)
      {
        return Path.Combine(Path.GetDirectoryName(path1) ?? string.Empty, Path.GetFileNameWithoutExtension(path1) + path2 + extension);
      }
      else
      {
        return path1 + path2;
      }
    }

    /// <summary>
    /// Rollover the file(s) to date/time tagged file(s).
    /// </summary>
    /// <param name="fileIsOpen">set to true if the file to be rolled is currently open</param>
    /// <remarks>
    /// <para>
    /// Rollover the file(s) to date/time tagged file(s).
    /// Resets curSizeRollBackups. 
    /// If fileIsOpen is set then the new file is opened (through SafeOpenFile).
    /// </para>
    /// </remarks>
    protected void RollOverTime(bool fileIsOpen)
    {
      if (StaticLogFileName)
      {
        // Compute filename, but only if datePattern is specified
        if (DatePattern == null)
        {
          ErrorHandler.Error("Missing DatePattern option in rollOver().");
          return;
        }

        //is the new file name equivalent to the 'current' one
        //something has gone wrong if we hit this -- we should only
        //roll over if the new file will be different from the old
        var dateFormat = m_now.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo);
        if (string.Equals(m_scheduledFilename, CombinePath(File!, dateFormat)))
        {
            ErrorHandler.Error("Compare " + m_scheduledFilename + " : " + CombinePath(File!, dateFormat));
            return;
        }

        if (fileIsOpen)
        {
          // close current file, and rename it to datedFilename
          CloseFile();
        }

        //we may have to roll over a large number of backups here
        for (var i = 1; i <= m_curSizeRollBackups; i++)
        {
          var from = CombinePath(File!, "." + i);
          var to = CombinePath(m_scheduledFilename!, "." + i);
          RollFile(from, to);
        }

        RollFile(File!, m_scheduledFilename!);
      }

      //We've cleared out the old date and are ready for the new
      m_curSizeRollBackups = 0;

      //new scheduled name
      m_scheduledFilename = CombinePath(File!, m_now.ToString(DatePattern, DateTimeFormatInfo.InvariantInfo));

      if (fileIsOpen)
      {
        // This will also close the file. This is OK since multiple close operations are safe.
        SafeOpenFile(m_baseFileName!, false);
      }
    }

    /// <summary>
    /// Renames file <paramref name="fromFile"/> to file <paramref name="toFile"/>.
    /// </summary>
    /// <param name="fromFile">Name of existing file to roll.</param>
    /// <param name="toFile">New name for file.</param>
    /// <remarks>
    /// <para>
    /// Renames file <paramref name="fromFile"/> to file <paramref name="toFile"/>. It
    /// also checks for existence of target file and deletes if it does.
    /// </para>
    /// </remarks>
    protected void RollFile(string fromFile, string toFile)
    {
      if (FileExists(fromFile))
      {
        // Delete the toFile if it exists
        DeleteFile(toFile);

        // We may not have permission to move the file, or the file may be locked
        try
        {
          LogLog.Debug(declaringType, "Moving [" + fromFile + "] -> [" + toFile + "]");
          using (SecurityContext?.Impersonate(this))
          {
            System.IO.File.Move(fromFile, toFile);
          }
        }
        catch (Exception moveEx)
        {
          ErrorHandler.Error("Exception while rolling file [" + fromFile + "] -> [" + toFile + "]", moveEx, ErrorCode.GenericFailure);
        }
      }
      else
      {
        LogLog.Warn(declaringType, "Cannot RollFile [" + fromFile + "] -> [" + toFile + "]. Source does not exist");
      }
    }

    /// <summary>
    /// Test if a file exists at a specified path
    /// </summary>
    /// <param name="path">the path to the file</param>
    /// <returns>true if the file exists</returns>
    /// <remarks>
    /// <para>
    /// Test if a file exists at a specified path
    /// </para>
    /// </remarks>
    protected bool FileExists(string path)
    {
      using (SecurityContext?.Impersonate(this))
      {
        return System.IO.File.Exists(path);
      }
    }

    /// <summary>
    /// Deletes the specified file if it exists.
    /// </summary>
    /// <param name="fileName">The file to delete.</param>
    /// <remarks>
    /// <para>
    /// Delete a file if it exists.
    /// The file is first moved to a new filename then deleted.
    /// This allows the file to be removed even when it cannot
    /// be deleted, but it still can be moved.
    /// </para>
    /// </remarks>
    protected void DeleteFile(string fileName)
    {
      if (FileExists(fileName))
      {
        // We may not have permission to delete the file, or the file may be locked

        var fileToDelete = fileName;

        // Try to move the file to temp name.
        // If the file is locked we may still be able to move it
        var tempFileName = fileName + "." + Environment.TickCount + ".DeletePending";
        try
        {
          using (SecurityContext?.Impersonate(this))
          {
            System.IO.File.Move(fileName, tempFileName);
          }
          fileToDelete = tempFileName;
        }
        catch (Exception moveEx)
        {
          LogLog.Debug(declaringType, "Exception while moving file to be deleted [" + fileName + "] -> [" + tempFileName + "]", moveEx);
        }

        // Try to delete the file (either the original or the moved file)
        try
        {
          using (SecurityContext?.Impersonate(this))
          {
            System.IO.File.Delete(fileToDelete);
          }
          LogLog.Debug(declaringType, "Deleted file [" + fileName + "]");
        }
        catch (Exception deleteEx)
        {
          if (fileToDelete == fileName)
          {
            // Unable to move or delete the file
            ErrorHandler.Error("Exception while deleting file [" + fileToDelete + "]", deleteEx, ErrorCode.GenericFailure);
          }
          else
          {
            // Moved the file, but the delete failed. File is probably locked.
            // The file should automatically be deleted when the lock is released.
            LogLog.Debug(declaringType, "Exception while deleting temp file [" + fileToDelete + "]", deleteEx);
          }
        }
      }
    }

    /// <summary>
    /// Implements file roll base on file size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the maximum number of size based backups is reached
    /// (<c>curSizeRollBackups == maxSizeRollBackups</c>) then the oldest
    /// file is deleted -- its index determined by the sign of countDirection.
    /// If <c>countDirection</c> &lt; 0, then files
    /// {<c>File.1</c>, ..., <c>File.curSizeRollBackups -1</c>}
    /// are renamed to {<c>File.2</c>, ...,
    /// <c>File.curSizeRollBackups</c>}. Moreover, <c>File</c> is
    /// renamed <c>File.1</c> and closed.
    /// </para>
    /// <para>
    /// A new file is created to receive further log output.
    /// </para>
    /// <para>
    /// If <c>maxSizeRollBackups</c> is equal to zero, then the
    /// <c>File</c> is truncated with no backup files created.
    /// </para>
    /// <para>
    /// If <c>maxSizeRollBackups</c> &lt; 0, then <c>File</c> is
    /// renamed if needed and no files are deleted.
    /// </para>
    /// </remarks>
    protected void RollOverSize()
    {
      CloseFile(); // keep windows happy.

      LogLog.Debug(declaringType, "rolling over count [" + ((CountingQuietTextWriter)QuietWriter).Count + "]");
      LogLog.Debug(declaringType, "maxSizeRollBackups [" + MaxSizeRollBackups + "]");
      LogLog.Debug(declaringType, "curSizeRollBackups [" + m_curSizeRollBackups + "]");
      LogLog.Debug(declaringType, "countDirection [" + CountDirection + "]");

      if (File is not null)
      {
          RollOverRenameFiles(File);
      }

      if (!StaticLogFileName && CountDirection >= 0)
      {
        m_curSizeRollBackups++;
      }

      // This will also close the file. This is OK since multiple close operations are safe.
      SafeOpenFile(m_baseFileName!, false);
    }

    /// <summary>
    /// Implements file roll.
    /// </summary>
    /// <param name="baseFileName">the base name to rename</param>
    /// <remarks>
    /// <para>
    /// If the maximum number of size based backups is reached
    /// (<c>curSizeRollBackups == maxSizeRollBackups</c>) then the oldest
    /// file is deleted -- its index determined by the sign of countDirection.
    /// If <c>countDirection</c> &lt; 0, then files
    /// {<c>File.1</c>, ..., <c>File.curSizeRollBackups -1</c>}
    /// are renamed to {<c>File.2</c>, ...,
    /// <c>File.curSizeRollBackups</c>}. 
    /// </para>
    /// <para>
    /// If <c>maxSizeRollBackups</c> is equal to zero, then the
    /// <c>File</c> is truncated with no backup files created.
    /// </para>
    /// <para>
    /// If <c>maxSizeRollBackups</c> &lt; 0, then <c>File</c> is
    /// renamed if needed and no files are deleted.
    /// </para>
    /// <para>
    /// This is called by <see cref="RollOverSize"/> to rename the files.
    /// </para>
    /// </remarks>
    protected void RollOverRenameFiles(string baseFileName)
    {
      // If maxBackups <= 0, then there is no file renaming to be done.
      if (MaxSizeRollBackups != 0)
      {
        if (CountDirection < 0)
        {
          // Delete the oldest file, to keep Windows happy.
          if (m_curSizeRollBackups == MaxSizeRollBackups)
          {
            DeleteFile(CombinePath(baseFileName, "." + MaxSizeRollBackups));
            m_curSizeRollBackups--;
          }

          // Map {(maxBackupIndex - 1), ..., 2, 1} to {maxBackupIndex, ..., 3, 2}
          for (var i = m_curSizeRollBackups; i >= 1; i--)
          {
            RollFile((CombinePath(baseFileName, "." + i)), (CombinePath(baseFileName, "." + (i + 1))));
          }

          m_curSizeRollBackups++;

          // Rename fileName to fileName.1
          RollFile(baseFileName, CombinePath(baseFileName, ".1"));
        }
        else
        {
          //countDirection >= 0
          if (m_curSizeRollBackups >= MaxSizeRollBackups && MaxSizeRollBackups > 0)
          {
            //delete the first and keep counting up.
            var oldestFileIndex = m_curSizeRollBackups - MaxSizeRollBackups;

            // If static then there is 1 file without a number, therefore 1 less archive
            if (StaticLogFileName)
            {
              oldestFileIndex++;
            }

            // If using a static log file then the base for the numbered sequence is the baseFileName passed in
            // If not using a static log file then the baseFileName will already have a numbered postfix which
            // we must remove, however it may have a date postfix which we must keep!
            var archiveFileBaseName = baseFileName;
            if (!StaticLogFileName)
            {
              if (PreserveLogFileNameExtension)
              {
                var extension = Path.GetExtension(archiveFileBaseName);
                var baseName = Path.GetFileNameWithoutExtension(archiveFileBaseName);
                var lastDotIndex = baseName.LastIndexOf(".", StringComparison.Ordinal);
                if (lastDotIndex >= 0)
                {
                  archiveFileBaseName = baseName.Substring(0, lastDotIndex) + extension;
                }
              }
              else
              {
                var lastDotIndex = archiveFileBaseName.LastIndexOf(".", StringComparison.Ordinal);
                if (lastDotIndex >= 0)
                {
                  archiveFileBaseName = archiveFileBaseName.Substring(0, lastDotIndex);
                }
              }
            }

            // Delete the archive file
            DeleteFile(CombinePath(archiveFileBaseName, "." + oldestFileIndex));
          }

          if (StaticLogFileName)
          {
            m_curSizeRollBackups++;
            RollFile(baseFileName, CombinePath(baseFileName, "." + m_curSizeRollBackups));
          }
        }
      }
    }

    #endregion

    #region NextCheckDate

    /// <summary>
    /// Get the start time of the next window for the current rollpoint
    /// </summary>
    /// <param name="currentDateTime">the current date</param>
    /// <param name="rollPoint">the type of roll point we are working with</param>
    /// <returns>the start time for the next roll point an interval after the currentDateTime date</returns>
    /// <remarks>
    /// <para>
    /// Returns the date of the next roll point after the currentDateTime date passed to the method.
    /// </para>
    /// <para>
    /// The basic strategy is to subtract the time parts that are less significant
    /// than the rollpoint from the current time. This should roll the time back to
    /// the start of the time window for the current rollpoint. Then we add 1 window
    /// worth of time and get the start time of the next window for the rollpoint.
    /// </para>
    /// </remarks>
    protected DateTime NextCheckDate(DateTime currentDateTime, RollPoint rollPoint)
    {
      // Local variable to work on (this does not look very efficient)
      var current = currentDateTime;

      // Do slightly different things depending on what the type of roll point we want.
      switch (rollPoint)
      {
        case RollPoint.TopOfMinute:
          current = current.AddMilliseconds(-current.Millisecond);
          current = current.AddSeconds(-current.Second);
          current = current.AddMinutes(1);
          break;

        case RollPoint.TopOfHour:
          current = current.AddMilliseconds(-current.Millisecond);
          current = current.AddSeconds(-current.Second);
          current = current.AddMinutes(-current.Minute);
          current = current.AddHours(1);
          break;

        case RollPoint.HalfDay:
          current = current.AddMilliseconds(-current.Millisecond);
          current = current.AddSeconds(-current.Second);
          current = current.AddMinutes(-current.Minute);

          if (current.Hour < 12)
          {
            current = current.AddHours(12 - current.Hour);
          }
          else
          {
            current = current.AddHours(-current.Hour);
            current = current.AddDays(1);
          }
          break;

        case RollPoint.TopOfDay:
          current = current.AddMilliseconds(-current.Millisecond);
          current = current.AddSeconds(-current.Second);
          current = current.AddMinutes(-current.Minute);
          current = current.AddHours(-current.Hour);
          current = current.AddDays(1);
          break;

        case RollPoint.TopOfWeek:
          current = current.AddMilliseconds(-current.Millisecond);
          current = current.AddSeconds(-current.Second);
          current = current.AddMinutes(-current.Minute);
          current = current.AddHours(-current.Hour);
          current = current.AddDays(7 - (int)current.DayOfWeek);
          break;

        case RollPoint.TopOfMonth:
          current = current.AddMilliseconds(-current.Millisecond);
          current = current.AddSeconds(-current.Second);
          current = current.AddMinutes(-current.Minute);
          current = current.AddHours(-current.Hour);
          current = current.AddDays(1 - current.Day); /* first day of month is 1 not 0 */
          current = current.AddMonths(1);
          break;
      }
      return current;
    }

    #endregion

    #region Private Instance Fields

    /// <summary>
    /// The actual formatted filename that is currently being written to
    /// or will be the file transferred to on roll over
    /// (based on staticLogFileName).
    /// </summary>
    private string? m_scheduledFilename;

    /// <summary>
    /// The timestamp when we shall next recompute the filename.
    /// </summary>
    private DateTime m_nextCheck = DateTime.MaxValue;

    /// <summary>
    /// Holds date of last roll over
    /// </summary>
    private DateTime m_now;

    /// <summary>
    /// The type of rolling done
    /// </summary>
    private RollPoint m_rollPoint;

    /// <summary>
    /// How many sized based backups have been made so far
    /// </summary>
    internal int m_curSizeRollBackups;

    /// <summary>
    /// The rolling mode used in this appender.
    /// </summary>
    private RollingMode m_rollingStyle = RollingMode.Composite;

    /// <summary>
    /// Cache flag set if we are rolling by date.
    /// </summary>
    private bool m_rollDate = true;

    /// <summary>
    /// Cache flag set if we are rolling by size.
    /// </summary>
    private bool m_rollSize = true;

    /// <summary>
    /// FileName provided in configuration.  Used for rolling properly
    /// </summary>
    private string? m_baseFileName;

    /// <summary>
    /// A mutex that is used to lock rolling of files.
    /// </summary>
    private Mutex? m_mutexForRolling;

    #endregion Private Instance Fields

    #region Static Members

    /// <summary>
    /// The 1st of January 1970 in UTC
    /// </summary>
    private static readonly DateTime s_date1970 = new DateTime(1970, 1, 1);

    #endregion

    #region DateTime

    /// <summary>
    /// This interface is used to supply Date/Time information to the <see cref="RollingFileAppender"/>.
    /// </summary>
    /// <remarks>
    /// This interface is used to supply Date/Time information to the <see cref="RollingFileAppender"/>.
    /// Used primarily to allow test classes to plug themselves in so they can
    /// supply test date/times.
    /// </remarks>
    public interface IDateTime
    {
      /// <summary>
      /// Gets the <i>current</i> time.
      /// </summary>
      /// <value>The <i>current</i> time.</value>
      /// <remarks>
      /// <para>
      /// Gets the <i>current</i> time.
      /// </para>
      /// </remarks>
      DateTime Now { get; }
    }

    /// <summary>
    /// Default implementation of <see cref="IDateTime"/> that returns the current time.
    /// </summary>
    private sealed class LocalDateTime : IDateTime
    {
      /// <summary>
      /// Gets the <b>current</b> time.
      /// </summary>
      /// <value>The <b>current</b> time.</value>
      /// <remarks>
      /// <para>
      /// Gets the <b>current</b> time.
      /// </para>
      /// </remarks>
      public DateTime Now
      {
        get { return DateTime.Now; }
      }
    }

    /// <summary>
    /// Implementation of <see cref="IDateTime"/> that returns the current time as the coordinated universal time (UTC).
    /// </summary>
    private sealed class UniversalDateTime : IDateTime
    {
      /// <summary>
      /// Gets the <b>current</b> time.
      /// </summary>
      /// <value>The <b>current</b> time.</value>
      /// <remarks>
      /// <para>
      /// Gets the <b>current</b> time.
      /// </para>
      /// </remarks>
      public DateTime Now
      {
        get { return DateTime.UtcNow; }
      }
    }

    #endregion DateTime
  }
}
