﻿using Kangaroo.Queries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.NetworkInformation;

namespace Kangaroo;

public sealed class ScannerBuilder : IScannerIpConfiguration, IScannerOptions, IScannerTimeoutNext, IScannerTtlNext, IScannerParallelNext
{
    /// <summary>
    /// Starts the scanner configuration process.
    /// </summary>
    /// <returns>Returns a new scanner builder and begins the build pipeline</returns>
    public static IScannerIpConfiguration Configure()
    {
        return new ScannerBuilder();
    }

    private readonly ScannerOptions _options = new();

    /// <inheritdoc />
    public IScannerOptions WithSubnet(IPAddress address, IPAddress subnetMask)
    {
        _options.IpAddresses = AddressFactory.CreateAddressesFromSubnet(address, subnetMask);
        return this;
    }
    
    public IScannerOptions WithSubnet(string address, string subnetMask)
    {
        _options.IpAddresses = AddressFactory.CreateAddressesFromSubnet(IPAddress.Parse(address), IPAddress.Parse(subnetMask));
        return this;
    }

    /// <inheritdoc />
    public IScannerOptions WithRange(IPAddress begin, IPAddress end)
    {
        _options.IpAddresses = AddressFactory.CreateAddressesFromSubnet(begin, end);
        return this;
    }
    
    /// <inheritdoc />
    public IScannerOptions WithRange(string begin, string end)
    {
        _options.IpAddresses = AddressFactory.CreateAddressesFromSubnet(IPAddress.Parse(begin), IPAddress.Parse(end));
        return this;
    }

    /// <inheritdoc />
    public IScannerOptions WithAddresses(IEnumerable<IPAddress> addresses)
    {
        _options.IpAddresses = addresses;
        return this;
    }

    /// <inheritdoc />
    public IScannerOptions WithInterface(NetworkInterface? @interface = null)
    {
        _options.IpAddresses = @interface != null
            ? AddressFactory.CreateAddressesFromInterface(@interface)
            : AddressFactory.CreateAddressesFromInterfaces();

        return this;
    }

    /// <inheritdoc />
    public IScannerTimeoutNext WithMaxTimeout(TimeSpan timeout)
    {
        switch (timeout.TotalMilliseconds)
        {
            case 0:
                throw new InvalidTimeoutException(timeout);
            case > 20_000:
                throw new InvalidTimeoutException(timeout);
            default:
                _options.Timeout = timeout;
                return this;
        }
    }

    /// <inheritdoc />
    public IScannerTtlNext WithMaxHops(int ttl = 10)
    {
        _options.TimeToLive = ttl;
        return this;
    }

    /// <inheritdoc />
    public IScannerParallelNext WithParallelism(int numberOfBatches = 10)
    {
        if (numberOfBatches == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfBatches));
        }
        _options.Concurrent = true;
        _options.ItemsPerBatch = numberOfBatches;
        return this;
    }

    /// <inheritdoc />
    public IScannerBuilder WithLogging(ILogger logger)
    {
        _options.Logger = logger;
        return this;
    }

    /// <inheritdoc />
    public IScannerBuilder WithLogging(Func<ILogger> loggerFactory)
    {
        _options.Logger = loggerFactory.Invoke();
        return this;
    }

    /// <inheritdoc />
    public IScannerBuilder WithLogging(ILoggerProvider loggerProvider)
    {
        ILoggerFactory fac = new NullLoggerFactory();
        fac.AddProvider(provider: loggerProvider);
        _options.Logger = fac.CreateLogger<IScanner>();
        return this;
    }

    /// <inheritdoc />
    public IScannerBuilder WithLogging(ILoggerFactory loggerFactory)
    {
        _options.Logger = loggerFactory.CreateLogger<IScanner>();
        return this;
    }

    /// <inheritdoc />
    public IScanner Build()
    {
        var queryOptions = new QueryOptions(_options.TimeToLive, _options.Timeout);
        
        var querier = new NetworkQuerierFactory(
            logger: _options.Logger,
            ping: _options.Concurrent
                ? new QueryPingResultsParallel(_options.Logger, queryOptions)
                : new QueryPingResultsOrderly(_options.Logger, new Ping(), queryOptions))
            .CreateQuerier();

        return !_options.Concurrent 
            ? CreateOrderlyScanner(querier, _options.IpAddresses)
            : CreateParallelScanner(querier, _options.IpAddresses, _options.ItemsPerBatch);
    }

    private IScanner CreateParallelScanner(
        IQueryNetworkNode querier,
        IEnumerable<IPAddress> addresses, 
        int batchSize) =>
        ParallelScanner.CreateScanner(
            _options.Logger,
            querier,
            addresses,
            batchSize);

    private IScanner CreateOrderlyScanner(
        IQueryNetworkNode querier,
        IEnumerable<IPAddress> addresses) =>
        OrderlyScanner.CreateScanner(
            _options.Logger,
            querier,
            addresses);
}