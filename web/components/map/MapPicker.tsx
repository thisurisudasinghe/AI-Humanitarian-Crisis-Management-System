"use client";
import {MapContainer,TileLayer,Marker, useMapEvents} from "react-leaflet";
import {useState} from "react";
import L from "leaflet";
const markerIcon=L.icon({iconUrl:"https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",iconRetinaUrl:"https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",shadowUrl:"https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",iconSize:[25,41],iconAnchor:[12,41]});
function PickerEvents({onChange}:{onChange:(p:{lat:number,lng:number})=>void}){useMapEvents({click(e){onChange({lat:e.latlng.lat,lng:e.latlng.lng})}});return null}
export default function MapPicker({value,onChange}:{value:{lat:number,lng:number},onChange:(p:{lat:number,lng:number})=>void}){return <div className="map"><MapContainer center={[value.lat,value.lng]} zoom={8} style={{height:"100%",width:"100%"}}><TileLayer attribution='&copy; OpenStreetMap contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"/><PickerEvents onChange={onChange}/><Marker position={[value.lat,value.lng]} icon={markerIcon}/></MapContainer></div>}
