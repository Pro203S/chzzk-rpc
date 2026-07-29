import css from './page.module.css';
import useSocket from '../../lib/ws/useSocket';
import { useEffect } from 'react';
import Header from '../../components/header';

export default function Main() {
    const socket = useSocket();

    useEffect(() => {
        (async () => {

        })();
    }, []);

    return <div className={css.container}>
        <Header />
        <div className={css.discord}>

        </div>
    </div>;
}
