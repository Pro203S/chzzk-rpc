import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import css from './styles.module.css';
import { faLink } from '@fortawesome/free-solid-svg-icons';
import useSocket from '../../lib/ws/useSocket';

export default function Header() {
    const socket = useSocket();

    return <div className={css.header}>
        <div className={css.logo}>
            <img src="./icons/icon128.png" draggable={false} />
            <span>Discheese</span>
        </div>
        <button className={css.status} data-connected={socket.connected ? "yes" : "no"}>
            <FontAwesomeIcon icon={faLink} />
        </button>
    </div>;
}