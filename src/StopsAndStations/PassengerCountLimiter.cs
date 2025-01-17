﻿// <copyright file="PassengerCountLimiter.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace StopsAndStations
{
    using System;
    using System.Linq;
    using ColossalFramework.Plugins;
    using ICities;

    /// <summary>
    /// A service that observes the stops in the city and calculates their current passenger count.
    /// Based on the provided <see cref="ModConfiguration"/>, it also limits the number of citizens
    /// waiting for transport at those stops by setting the passenger status to 'cannot use transport'.
    /// </summary>
    public sealed class PassengerCountLimiter : ThreadingExtensionBase
    {
        private const int StepMask = 0xF;
        private const int StepSize = CitizenManager.MAX_INSTANCE_COUNT / (StepMask + 1);
        private const CitizenInstance.Flags InstanceUsingTransport = CitizenInstance.Flags.OnPath | CitizenInstance.Flags.WaitingTransport;

        private readonly ushort[] passengerCount = new ushort[NetManager.MAX_NODE_COUNT];
        private readonly NetSegment[] segments;
        private readonly CitizenInstance[] instances;
        private readonly PathUnit[] pathUnits;
        private readonly NetNode[] nodes;
        private readonly TransportLine[] transportLines;

        private ModConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="PassengerCountLimiter"/> class.
        /// </summary>
        public PassengerCountLimiter()
        {
            segments = NetManager.instance.m_segments.m_buffer;
            instances = CitizenManager.instance.m_instances.m_buffer;
            pathUnits = PathManager.instance.m_pathUnits.m_buffer;
            nodes = NetManager.instance.m_nodes.m_buffer;
            transportLines = TransportManager.instance.m_lines.m_buffer;
        }

        /// <summary>
        /// A method that is called by the game after this instance is created.
        /// </summary>
        /// <param name="threading">A reference to the game's <see cref="IThreading"/> implementation.</param>
        public override void OnCreated(IThreading threading)
        {
            var mod = PluginManager.instance.GetPluginsInfo()
                .Select(p => p.userModInstance)
                .OfType<StopsAndStationsMod>()
                .FirstOrDefault();

            configuration = mod?.ConfigProvider.Configuration ?? new ModConfiguration();
        }

        /// <summary>
        /// A method that is called by the game before each simulation tick.
        /// Each tick contains multiple frames.
        /// Calculates the passenger count for every transport line stop.
        /// </summary>
        public override void OnBeforeSimulationTick()
        {
            Array.Clear(passengerCount, 0, passengerCount.Length);

            for (int i = 0; i < instances.Length; ++i)
            {
                ref var instance = ref instances[i];
                var pathId = instance.m_path;
                if (pathId != 0 && (instance.m_flags & InstanceUsingTransport) == InstanceUsingTransport)
                {
                    var pathPosition = pathUnits[pathId].GetPosition(instance.m_pathPositionIndex >> 1);
                    ushort nodeId = segments[pathPosition.m_segment].m_startNode;
                    ++passengerCount[nodeId];
                }
            }
        }

        /// <summary>
        /// A method that is called by the game before each simulation frame.
        /// </summary>
        public override void OnBeforeSimulationFrame()
        {
            var step = SimulationManager.instance.m_currentFrameIndex & StepMask;
            var startIndex = step * StepSize;
            var endIndex = (step + 1) * StepSize;

            for (var i = startIndex; i < endIndex; ++i)
            {
                ref var instance = ref instances[i];
                var pathId = instance.m_path;
                if (pathId != 0
                    && instance.m_waitCounter == 0
                    && (instance.m_flags & InstanceUsingTransport) == InstanceUsingTransport)
                {
                    var pathPosition = pathUnits[pathId].GetPosition(instance.m_pathPositionIndex >> 1);
                    ushort nodeId = segments[pathPosition.m_segment].m_startNode;
                    if (passengerCount[nodeId] > GetMaximumAllowedPassengers(nodeId))
                    {
                        --passengerCount[nodeId];
                        instance.m_flags |= CitizenInstance.Flags.BoredOfWaiting;
                        instance.m_waitCounter = byte.MaxValue;
                    }
                }
            }
        }

        private int GetMaximumAllowedPassengers(ushort nodeId)
        {
            var transportLineId = nodes[nodeId].m_transportLine;
            if (transportLineId == 0)
            {
                return int.MaxValue;
            }

            switch (transportLines[transportLineId].Info?.m_transportType)
            {
                case TransportInfo.TransportType.EvacuationBus:
                    return configuration.MaxWaitingPassengersEvacuationBus;

                case TransportInfo.TransportType.Bus:
                    return configuration.MaxWaitingPassengersBus;

                case TransportInfo.TransportType.TouristBus:
                    return configuration.MaxWaitingPassengersTouristBus;

                case TransportInfo.TransportType.Tram:
                    return configuration.MaxWaitingPassengersTram;

                case TransportInfo.TransportType.Metro:
                    return configuration.MaxWaitingPassengersMetro;

                case TransportInfo.TransportType.Train:
                    return configuration.MaxWaitingPassengersTrain;

                case TransportInfo.TransportType.Monorail:
                    return configuration.MaxWaitingPassengersMonorail;

                case TransportInfo.TransportType.Airplane:
                    return configuration.MaxWaitingPassengersAirplane;

                case TransportInfo.TransportType.Ship:
                    return configuration.MaxWaitingPassengersShip;

                case TransportInfo.TransportType.CableCar:
                    return configuration.MaxWaitingPassengersCableCar;

                case TransportInfo.TransportType.HotAirBalloon:
                    return configuration.MaxWaitingPassengersHotAirBalloon;

                default:
                    return int.MaxValue;
            }
        }
    }
}
