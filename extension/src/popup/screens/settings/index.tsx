import css from './page.module.css';
import useSocket from '../../lib/ws/useSocket';
import { useEffect } from 'react';
import Header from '../../components/header';

export default function Settings() {
    const socket = useSocket();

    useEffect(() => {
        (async () => {

        })();
    }, []);

    return <div className={css.container}>
        <Header />
        <div className={css.section}>
            <span style={{ "color": "#fff" }}>{JSON.stringify(socket)}</span>
        </div>
    </div>;
}
