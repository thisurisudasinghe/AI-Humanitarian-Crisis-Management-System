const API = process.env.NEXT_PUBLIC_API_BASE_URL ?? "https://localhost:7001";
export async function api<T>(path:string, init:RequestInit={}):Promise<T>{
 const token=typeof window!=="undefined"?localStorage.getItem("accessToken"):null;
 const response=await fetch(`${API}/api/v1${path}`,{...init,headers:{"Content-Type":"application/json",...(token?{Authorization:`Bearer ${token}`} : {}),...init.headers}});
 if(!response.ok){const problem=await response.json().catch(()=>({message:response.statusText}));throw new Error(problem.message??problem.code??"Request failed");}
 return response.status===204?undefined as T:response.json();
}
