import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import css from './styles.module.css';
import { faGear, faHome, faLink } from '@fortawesome/free-solid-svg-icons';
import useSocket from '../../lib/ws/useSocket';
import useNavigate from '../../lib/context/useNavigate';

export default function Header() {
    const [navigate, current] = useNavigate();
    const socket = useSocket();

    return <div className={css.header}>
        <div className={css.logo}>
            <img src="./icons/icon128.png" draggable={false} />
            <span>Discheese</span>
        </div>
        <div className={css.buttons}>
            <button
                title={socket.connected ? "서버 연결됨" : "서버 연결 안됨"}
                className={css.status}
                data-connected={socket.connected ? "yes" : "no"}
            >
                <FontAwesomeIcon icon={faLink} />
            </button>
            <button
                title={current === "main" ? "설정" : "홈"}
                className={css.navigate}
                onClick={() => {
                    if (current === "main") {
                        navigate("settings");
                        return;
                    }

                    navigate("main");
                    return;
                }}
            >
                <FontAwesomeIcon icon={current === "main" ? faGear : faHome} />
            </button>
        </div>
    </div>;
}