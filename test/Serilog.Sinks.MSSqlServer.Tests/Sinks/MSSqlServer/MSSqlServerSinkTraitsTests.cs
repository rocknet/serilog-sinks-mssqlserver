﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using Moq;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;
using Serilog.Sinks.MSSqlServer.Platform;
using Serilog.Sinks.MSSqlServer.Sinks.MSSqlServer.Platform;
using Serilog.Sinks.MSSqlServer.Tests.TestUtils;
using Xunit;

namespace Serilog.Sinks.MSSqlServer.Tests.Sinks.MSSqlServer
{
    [Trait(TestCategory.TraitName, TestCategory.Unit)]
    public class MSSqlServerSinkTraitsTests : IDisposable
    {
        private readonly Mock<ISqlConnectionFactory> _sqlConnectionFactoryMock;
        private readonly string _tableName = "tableName";
        private readonly string _schemaName = "schemaName";
        private MSSqlServerSinkTraits _sut;
        private bool _disposedValue;

        public MSSqlServerSinkTraitsTests()
        {
            _sqlConnectionFactoryMock = new Mock<ISqlConnectionFactory>();
        }

        [Trait("Bugfix", "#187")]
        [Fact]
        public void GetColumnsAndValuesCreatesTimeStampOfTypeDateTimeAccordingToColumnOptions()
        {
            // Arrange
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions();
            var testDateTimeOffset = new DateTimeOffset(2020, 1, 1, 9, 0, 0, new TimeSpan(1, 0, 0)); // Timezone +1:00
            var logEvent = CreateLogEvent(testDateTimeOffset);
            SetupSut(options);

            // Act
            var columns = _sut.GetColumnsAndValues(logEvent);

            // Assert
            var timeStampColumn = columns.Single(c => c.Key == options.TimeStamp.ColumnName);
            Assert.IsType<DateTime>(timeStampColumn.Value);
            Assert.Equal(testDateTimeOffset.Hour, ((DateTime)timeStampColumn.Value).Hour);
        }

        [Trait("Bugfix", "#187")]
        [Fact]
        public void GetColumnsAndValuesCreatesUtcConvertedTimeStampOfTypeDateTimeAccordingToColumnOptions()
        {
            // Arrange
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions
            {
                TimeStamp = { ConvertToUtc = true }
            };
            var testDateTimeOffset = new DateTimeOffset(2020, 1, 1, 9, 0, 0, new TimeSpan(1, 0, 0)); // Timezone +1:00
            var logEvent = CreateLogEvent(testDateTimeOffset);
            SetupSut(options);

            // Act
            var columns = _sut.GetColumnsAndValues(logEvent);

            // Assert
            var timeStampColumn = columns.Single(c => c.Key == options.TimeStamp.ColumnName);
            Assert.IsType<DateTime>(timeStampColumn.Value);
            Assert.Equal(testDateTimeOffset.Hour - 1, ((DateTime)timeStampColumn.Value).Hour);
        }

        [Trait("Bugfix", "#187")]
        [Fact]
        public void GetColumnsAndValuesCreatesTimeStampOfTypeDateTimeOffsetAccordingToColumnOptions()
        {
            // Arrange
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions
            {
                TimeStamp = { DataType = SqlDbType.DateTimeOffset }
            };
            var testDateTimeOffset = new DateTimeOffset(2020, 1, 1, 9, 0, 0, new TimeSpan(1, 0, 0)); // Timezone +1:00
            var logEvent = CreateLogEvent(testDateTimeOffset);
            SetupSut(options);

            // Act
            var columns = _sut.GetColumnsAndValues(logEvent);

            // Assert
            var timeStampColumn = columns.Single(c => c.Key == options.TimeStamp.ColumnName);
            Assert.IsType<DateTimeOffset>(timeStampColumn.Value);
            var timeStampColumnOffset = (DateTimeOffset)timeStampColumn.Value;
            Assert.Equal(testDateTimeOffset.Hour, timeStampColumnOffset.Hour);
            Assert.Equal(testDateTimeOffset.Offset, timeStampColumnOffset.Offset);
        }

        [Trait("Bugfix", "#187")]
        [Fact]
        public void GetColumnsAndValuesCreatesUtcConvertedTimeStampOfTypeDateTimeOffsetAccordingToColumnOptions()
        {
            // Arrange
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions
            {
                TimeStamp = { DataType = SqlDbType.DateTimeOffset, ConvertToUtc = true }
            };
            var testDateTimeOffset = new DateTimeOffset(2020, 1, 1, 9, 0, 0, new TimeSpan(1, 0, 0)); // Timezone +1:00
            var logEvent = CreateLogEvent(testDateTimeOffset);
            SetupSut(options);

            // Act
            var columns = _sut.GetColumnsAndValues(logEvent);

            // Assert
            var timeStampColumn = columns.Single(c => c.Key == options.TimeStamp.ColumnName);
            Assert.IsType<DateTimeOffset>(timeStampColumn.Value);
            var timeStampColumnOffset = (DateTimeOffset)timeStampColumn.Value;
            Assert.Equal(testDateTimeOffset.Hour - 1, timeStampColumnOffset.Hour);
            Assert.Equal(new TimeSpan(0), timeStampColumnOffset.Offset);
        }

        [Fact]
        public void GetColumnsAndValuesWhenCalledWithCustomFormatterRendersLogEventPropertyUsingCustomFormatter()
        {
            // Arrange
            const string testLogEventContent = "Content of LogEvent";
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions();
            options.Store.Add(StandardColumn.LogEvent);
            var logEventFormatterMock = new Mock<ITextFormatter>();
            logEventFormatterMock.Setup(f => f.Format(It.IsAny<LogEvent>(), It.IsAny<TextWriter>()))
                .Callback<LogEvent, TextWriter>((e, w) => w.Write(testLogEventContent));
            var logEvent = CreateLogEvent(DateTimeOffset.UtcNow);
            SetupSut(options, logEventFormatter: logEventFormatterMock.Object);

            // Act
            var columns = _sut.GetColumnsAndValues(logEvent);

            // Assert
            var logEventColumn = columns.Single(c => c.Key == options.LogEvent.ColumnName);
            Assert.Equal(testLogEventContent, logEventColumn.Value);
        }

        [Fact]
        public void GetColumnsAndValuesWhenCalledWithoutFormatterRendersLogEventPropertyUsingInternalJsonFormatter()
        {
            // Arrange
            const string expectedLogEventContent =
                "{\"TimeStamp\":\"2020-01-01T09:00:00.0000000\",\"Level\":\"Information\",\"Message\":\"\",\"MessageTemplate\":\"\"}";
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions();
            options.Store.Add(StandardColumn.LogEvent);
            var testDateTimeOffset = new DateTimeOffset(2020, 1, 1, 9, 0, 0, TimeSpan.Zero);
            var logEvent = CreateLogEvent(testDateTimeOffset);
            SetupSut(options);

            // Act
            var columns = _sut.GetColumnsAndValues(logEvent);

            // Assert
            var logEventColumn = columns.Single(c => c.Key == options.LogEvent.ColumnName);
            Assert.Equal(expectedLogEventContent, logEventColumn.Value);
        }

        [Fact]
        public void InitializeWithAutoCreateSqlTableCallsSqlTableCreator()
        {
            // Arrange
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions();
            var sqlTableCreatorMock = new Mock<ISqlTableCreator>();

            // Act
            SetupSut(options, autoCreateSqlTable: true, sqlTableCreator: sqlTableCreatorMock.Object);

            // Assert
            sqlTableCreatorMock.Verify(c => c.CreateTable(_schemaName, _tableName,
                It.IsAny<DataTable>(), options),
                Times.Once);
        }

        [Fact]
        public void InitializeWithoutAutoCreateSqlTableDoesNotCallSqlTableCreator()
        {
            // Arrange
            var options = new Serilog.Sinks.MSSqlServer.ColumnOptions();
            var sqlTableCreatorMock = new Mock<ISqlTableCreator>();

            // Act
            SetupSut(options, autoCreateSqlTable: false, sqlTableCreator: sqlTableCreatorMock.Object);

            // Assert
            sqlTableCreatorMock.Verify(c => c.CreateTable(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DataTable>(), It.IsAny<Serilog.Sinks.MSSqlServer.ColumnOptions>()),
                Times.Never);
        }

        private static LogEvent CreateLogEvent(DateTimeOffset testDateTimeOffset)
        {
            return new LogEvent(testDateTimeOffset, LogEventLevel.Information, null,
                new MessageTemplate(new List<MessageTemplateToken>()), new List<LogEventProperty>());
        }

        private void SetupSut(
            Serilog.Sinks.MSSqlServer.ColumnOptions options,
            bool autoCreateSqlTable = false,
            ITextFormatter logEventFormatter = null,
            ISqlTableCreator sqlTableCreator = null)
        {
            if (sqlTableCreator == null)
            {
                _sut = new MSSqlServerSinkTraits(_sqlConnectionFactoryMock.Object, _tableName, _schemaName,
                    options, CultureInfo.InvariantCulture, autoCreateSqlTable, logEventFormatter);
            }
            else
            {
                // Internal constructor to use ISqlTableCreator mock
                _sut = new MSSqlServerSinkTraits(_tableName, _schemaName, options, CultureInfo.InvariantCulture,
                    autoCreateSqlTable, logEventFormatter, sqlTableCreator);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _sut.Dispose();
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
