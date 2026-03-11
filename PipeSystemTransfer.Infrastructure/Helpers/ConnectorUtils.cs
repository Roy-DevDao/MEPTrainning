using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using PipeSystemTransfer.Core.Models;

namespace PipeSystemTransfer.Infrastructure.Helpers
{
    internal static class ConnectorUtils
    {
        internal static ConnectorManager GetConnectorManager(Element el)
        {
            if (el is Pipe pipe)             return pipe.ConnectorManager;
            if (el is FamilyInstance fi)     return fi.MEPModel?.ConnectorManager;
            return null;
        }

        internal static Connector FindConnectorById(ConnectorManager cm, int id)
        {
            foreach (Connector c in cm.Connectors)
                if (c.Domain == Domain.DomainPiping && c.Id == id) return c;
            return null;
        }

        internal static Connector FindFreeConnector(ConnectorManager cm)
        {
            foreach (Connector c in cm.Connectors)
                if (c.Domain == Domain.DomainPiping && !c.IsConnected) return c;
            return null;
        }

        internal static Connector FindClosestFreeConnector(ConnectorManager cm, XYZ origin)
        {
            Connector best     = null;
            double    bestDist = double.MaxValue;
            foreach (Connector c in cm.Connectors)
            {
                if (c.Domain != Domain.DomainPiping || c.IsConnected) continue;
                double dist = c.Origin.DistanceTo(origin);
                if (dist < bestDist) { bestDist = dist; best = c; }
            }
            return best;
        }

        internal static void TryConnect(
            string ownerId, ConnectorManager cmA, ConnectorDto connDto,
            Dictionary<string, Element> idToElement,
            HashSet<string> processed, ref int joined, List<string> errorLog)
        {
            if (string.IsNullOrEmpty(connDto.ConnectedToId)) return;

            var pairKey = string.Compare(ownerId, connDto.ConnectedToId, StringComparison.Ordinal) < 0
                ? $"{ownerId}:{connDto.ConnectedToId}"
                : $"{connDto.ConnectedToId}:{ownerId}";
            if (!processed.Add(pairKey)) return;

            if (!idToElement.TryGetValue(connDto.ConnectedToId, out var otherElement)) return;
            var cmB = GetConnectorManager(otherElement);
            if (cmB == null) return;

            var connA = FindConnectorById(cmA, connDto.ConnectorId) ?? FindFreeConnector(cmA);
            if (connA == null || connA.IsConnected) return;

            var connB = FindClosestFreeConnector(cmB, connA.Origin);
            if (connB == null || connB.IsConnected) return;

            try { connA.ConnectTo(connB); joined++; }
            catch (Exception ex)
            { errorLog.Add($"[Connect] {ownerId}↔{connDto.ConnectedToId} — {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
