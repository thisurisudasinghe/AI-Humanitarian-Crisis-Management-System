"use client";
import {useEffect,useState} from "react";import {api} from "../../../lib/api";
type Task={id:string,title:string,status:string,scheduledAt?:string};
export default function Volunteer(){const [tasks,setTasks]=useState<Task[]>([]);const [error,setError]=useState("");useEffect(()=>{api<Task[]>("/volunteer-tasks/mine").then(setTasks).catch(e=>setError(e.message))},[]);return <><h1>Volunteer workspace</h1><p>View assigned tasks and manage your availability through the volunteer API.</p>{error&&<p className="muted">{error}</p>}<section className="panel"><h2>My tasks</h2>{tasks.length?tasks.map(t=><p key={t.id}>{t.title} — {t.status}</p>):<p className="muted">No tasks returned. Sign in with a Volunteer account.</p>}</section></>}
