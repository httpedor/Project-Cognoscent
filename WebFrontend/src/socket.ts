import { compendium, type CompendiumFolder } from "./compendium";
import { DeviceType, ProtocolId, type CompendiumUpdatePacket, type EntityCreatePacket, type EntityRemovePacket, type EntityStatPacket, type Packet } from "./networking.generated";
import type { Entity, Stat } from "./rpg";
import { store } from "./store";

let socket: WebSocket;

export default function startWebSocket()
{
  socket = new WebSocket("ws://" + window.location.host + "/ws");
  socket.onopen = () => {
    console.log("WebSocket connection established.");
    console.log(socket.readyState);
    socket.send(JSON.stringify({
      id: ProtocolId.HANDSHAKE,
      username: store.username,
      device: DeviceType.MOBILE
    }))
  }

  socket.onerror = (e) => {
    console.log("WebSocket error: ", e);
  }

  socket.onclose = (e) => {
    console.log("WebSocket connection closed: ", e);
  }

  socket.onmessage = (event) => {
    const packet: Packet = JSON.parse(event.data)
    switch (packet.id)
    {
      case ProtocolId.COMPENDIUM_UPDATE:
      {
        const cuPacket: CompendiumUpdatePacket = packet as CompendiumUpdatePacket
        const folder = cuPacket.registryName as CompendiumFolder;
        const id = cuPacket.dataName;
        const entry = cuPacket.json;

        if (!cuPacket.remove)
        {
          if (!compendium[folder])
            compendium[folder] = {};
          compendium[folder][id] = entry;
        }
        else
          delete compendium[folder][id];
        break;
      }
      case ProtocolId.ENTITY_CREATE:
      {
        const ecPacket: EntityCreatePacket = packet as EntityCreatePacket
        store.entities.push(ecPacket.entity as Entity);
        break;
      }
      case ProtocolId.ENTITY_REMOVE:
      {
        const erPacket = packet as EntityRemovePacket;
        store.entities = store.entities.filter(e => e.id !== erPacket.ref.id);
        break;
      }
      case ProtocolId.ENTITY_STAT:
      {
        const esPacket = packet as EntityStatPacket;
        const entity = store.entities.find(e => e.id === esPacket.entityRef.id);
        if (entity)
        {
          entity.stats[esPacket.statId] = esPacket.stat as Stat;
        }
      }
      //TODO: Handle private message packet, I think just create a notification in the UI
      default:
      {
        console.warn('Unhandled packet id:', packet.id)
        break;
      }
    }
  }
}

export function sendPacket(packet: Packet)
{
  if (socket && socket.readyState === WebSocket.OPEN)
  {
    socket.send(JSON.stringify(packet));
  }
}
