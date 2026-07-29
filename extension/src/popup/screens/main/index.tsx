import css from './page.module.css';
import useSocket from '../../lib/ws/useSocket';
import { useEffect } from 'react';
import Header from '../../components/header';

export default function Main() {
    const socket = useSocket();

    useEffect(() => {
    }, []);

    return <div className={css.container}>
        <Header />
        <div className={css.connection}>

        </div>
    </div>;
}
