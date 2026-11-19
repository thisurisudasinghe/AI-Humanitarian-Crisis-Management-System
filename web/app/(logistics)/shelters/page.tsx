"use client";
import {useEffect,useState} from "react";import {api} from "../../../lib/api";
type Shelter={id:string,name:string,capacity:number,occupancy:number,freePlaces:number,lat:number,lng:number};
export default function Shelters(){const [rows,setRows]=useState<Shelter[]>([]);const [error,setError]=useState("");useEffect(()=>{api<Shelter[]>("/shelters").then(setRows).catch(e=>setError(e.message))},[]);return <><h1>Shelter management</h1>{error&&<p className="error" role="alert">{error}</p>}<div className="grid">{rows.map(s=><article className="panel" key={s.id}><h2>{s.name}</h2><p>{s.occupancy} / {s.capacity} occupied</p><progress value={s.occupancy} max={s.capacity}/><p>{s.freePlaces} places available</p></article>)}</div></>}
