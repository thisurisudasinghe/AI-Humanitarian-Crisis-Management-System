"use client";
import {MapContainer,TileLayer,Marker,Popup} from "react-leaflet";
import L from "leaflet";
const icon=L.icon({iconUrl:"https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",iconRetinaUrl:"https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",shadowUrl:"https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",iconSize:[25,41],iconAnchor:[12,41]});
export type MapMarker={id:string,name:string,lat:number,lng:number,description?:string};
export default function MapView({markers}:{markers:MapMarker[]}){const center: [number,number]=markers.length?[markers[0].lat,markers[0].lng]:[7.8731,80.7718];return <div className="map"><MapContainer center={center} zoom={7} style={{height:"100%",width:"100%"}}><TileLayer attribution='&copy; OpenStreetMap contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"/>{markers.map(m=><Marker key={m.id} position={[m.lat,m.lng]} icon={icon}><Popup><b>{m.name}</b><br/>{m.description}</Popup></Marker>)}</MapContainer></div>}
