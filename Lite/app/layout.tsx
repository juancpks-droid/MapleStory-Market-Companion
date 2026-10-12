import type {Metadata,Viewport} from 'next';
import './globals.css';
export const metadata:Metadata={title:'Maple Market Lite',description:'Item Finder and Price Checker for Classic World players.',manifest:'/manifest.webmanifest',appleWebApp:{capable:true,statusBarStyle:'black-translucent',title:'Maple Lite'},icons:{icon:'/icon.png',apple:'/icon.png'}};
export const viewport:Viewport={width:'device-width',initialScale:1,viewportFit:'cover',themeColor:'#101719'};
export default function RootLayout({children}:{children:React.ReactNode}){return <html lang="en"><body>{children}</body></html>}
