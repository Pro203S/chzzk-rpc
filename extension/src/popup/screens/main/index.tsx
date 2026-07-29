import css from './page.module.css';

export default function Main() {
    return <div className={css.container}>
        <div className={css.title}>
            <img src="/icons/icon128.png" />
        </div>
    </div>;
}