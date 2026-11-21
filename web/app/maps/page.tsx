"use client";
import dynamic from "next/dynamic";import {useEffect,useState} from "react";import {api} from "../../lib/api";
const MapView=dynamic(()=>import("../../components/map/MapView"),{ssr:false});
type Shelter={id:string,name:string,lat:number,lng:number,freePlaces:number};
export default function Maps(){const [items,setItems]=useState<Shelter[]>([]);useEffect(()=>{api<Shelter[]>("/shelters/public").then(setItems).catch(()=>setItems([]))},[]);return <><h1>Operations map</h1><p className="muted">Approved shelters. MapPicker can be embedded in location forms; manual coordinates remain available if geocoding is offline.</p><MapView markers={items.map(x=>({id:x.id,name:x.name,lat:x.lat,lng:x.lng,description:`${x.freePlaces} free places`}))}/></>}
