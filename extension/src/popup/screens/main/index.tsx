import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import css from './page.module.css';
import { faLink, faLinkSlash } from '@fortawesome/free-solid-svg-icons';
import useSocket from '../../lib/ws/useSocket';

export default function Main() {
    const socket = useSocket();

    return <div className={css.container}>
        <div className={css.title}>
            <img src="./icons/icon128.png" draggable={false} />
            <span>Discheese</span>
        </div>
        <div className={css.connection}>
            {socket.connected ? <>
                <FontAwesomeIcon icon={faLink} />
                <span>연결됨</span>
            </> : <>
                <FontAwesomeIcon icon={faLinkSlash} />
                <span>재시도</span>
            </>}
        </div>
    </div>;
}
