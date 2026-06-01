import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bot, Code2, ShieldCheck, Rocket } from 'lucide-react';
import PublicHeader from '../PublicHeader';

interface WelcomePageProps {
  isDarkMode: boolean;
  toggleTheme: () => void;
}

export function WelcomePage({ isDarkMode, toggleTheme }: WelcomePageProps) {
  const navigate = useNavigate();
  const [currentWordIndex, setCurrentWordIndex] = useState(0);

  const words = ['Design', 'Build', 'Test', 'Deploy'];
  const colors = ['text-[#a855f7]', 'text-[#22d3ee]', 'text-[#eab308]', 'text-[#10b981]'];
  const borders = [
    'border-[#a855f7]/50',
    'border-[#22d3ee]/50',
    'border-[#eab308]/50',
    'border-[#10b981]/50',
  ];

  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentWordIndex((prev) => (prev + 1) % words.length);
    }, 2500);

    return () => clearInterval(interval);
  }, []);

  return (
    <div className="relative flex min-h-screen flex-col overflow-hidden bg-background font-sans text-text transition-colors duration-300">
      <PublicHeader isDarkMode={isDarkMode} toggleTheme={toggleTheme} />

      {/* Main Content */}
      <main className="flex-grow relative flex flex-col items-center justify-center w-full">
        <div className="absolute inset-0 w-full flex flex-col items-center justify-center font-sans overflow-hidden transition-colors duration-300">
          <div className="text-center z-10 space-y-8 px-6 w-full max-w-5xl mt-[-60px] md:mt-[-100px]">
            {/* Hero Title */}
            <div className="flex flex-col md:flex-row items-center justify-center gap-2 md:gap-6 text-6xl md:text-8xl font-black tracking-tighter text-text">
              <span>We</span>
              <div className="w-full md:w-[320px] text-center md:text-left relative h-[80px] md:h-[100px] overflow-hidden flex items-center justify-center md:justify-start pt-2">
                <WordCarousel
                  words={words}
                  colors={colors}
                  currentIndex={currentWordIndex}
                />
              </div>
              <span>Code.</span>
            </div>

            {/* Subtitle */}
            <p className="text-lg md:text-2xl text-textMuted max-w-2xl mx-auto font-medium">
              Orchestra is a sleek, invisible layer of AI intelligence that turns your requirements into deployments
              seamlessly.
            </p>

            {/* Login Button */}
            <div className="pt-8 md:pt-12">
              <button
                onClick={() => navigate('/login')}
                className="rounded-full bg-primary px-8 py-4 text-lg font-bold text-white transition-all duration-300 hover:-translate-y-1 hover:bg-primaryHover hover:shadow-[0_0_30px_rgba(99,102,241,0.25)]"
              >
                Login to Orchestra
              </button>
            </div>

            {/* Workflow Section */}
            <div className="pt-12 lg:pt-20 max-w-3xl mx-auto hidden sm:block w-full">
              <WorkflowVisualization currentIndex={currentWordIndex} colors={colors} borders={borders} />
            </div>
          </div>

          {/* Background Blur */}
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] md:w-[800px] h-[600px] md:h-[800px] bg-cyan-400/5 dark:bg-cyan-500/10 blur-[100px] md:blur-[120px] rounded-full pointer-events-none -z-10"></div>
        </div>
      </main>
    </div>
  );
}

interface WordCarouselProps {
  words: string[];
  colors: string[];
  currentIndex: number;
}

const WordCarousel = ({ words, colors, currentIndex }: WordCarouselProps) => {
  const [previousIndex, setPreviousIndex] = useState(currentIndex);
  const previousIndexRef = useRef(currentIndex);

  useEffect(() => {
    if (previousIndexRef.current !== currentIndex) {
      setPreviousIndex(previousIndexRef.current);
      previousIndexRef.current = currentIndex;
    }
  }, [currentIndex]);

  return (
    <div className="relative w-full h-full overflow-hidden">
      {words.map((word, index) => {
        const isActive = index === currentIndex;
        const isOutgoing = index === previousIndex && !isActive;

        return (
          <span
            key={word}
            className={`absolute inset-0 flex items-center justify-center md:justify-start leading-none pb-2 transition-[opacity,transform] duration-500 ${colors[index]} ${
              isActive
                ? 'opacity-100 translate-y-0'
                : isOutgoing
                  ? 'opacity-0 -translate-y-8'
                  : 'opacity-0 translate-y-8'
            }`}
            style={{
              transitionTimingFunction: 'cubic-bezier(0.22, 1, 0.36, 1)',
            }}
          >
            {word}
          </span>
        );
      })}
    </div>
  );
};

interface WorkflowVisualizationProps {
  currentIndex: number;
  colors: string[];
  borders: string[];
}

const WorkflowVisualization = ({ currentIndex, colors, borders }: WorkflowVisualizationProps) => {
  const stages = [
    { icon: Bot, label: 'Product Manager AI', index: 0 },
    { icon: Code2, label: 'Developer AI', index: 1 },
    { icon: ShieldCheck, label: 'QA Auto-Tester AI', index: 2 },
    { icon: Rocket, label: 'DevOps AI', index: 3 },
  ];

  const progressWidth = (currentIndex / 3) * 100;

  return (
    <div className="relative flex justify-between items-start w-full">
      {/* Connecting Line */}
      <div className="absolute left-[64px] right-[64px] h-0.5 bg-border top-7 -translate-y-1/2 z-0">
        <div
          className="h-full bg-gradient-to-r from-[#a855f7] via-[#22d3ee] to-[#10b981] shadow-[0_0_10px_rgba(6,182,212,0.5)] transition-all duration-500"
          style={{ width: `${progressWidth}%` }}
        ></div>
      </div>

      {/* Stage Icons */}
      {stages.map((stage) => {
        const Icon = stage.icon;
        const isActive = stage.index === currentIndex;
        const isPast = stage.index < currentIndex;

        return (
          <div
            key={stage.index}
            className="relative z-10 flex flex-col items-center gap-4 group w-32"
          >
            <div
              className={`w-14 h-14 rounded-full border-2 flex items-center justify-center bg-surface transition-all duration-300 relative ${
                isActive ? `${borders[stage.index]} scale-[1.15]` : isPast ? `${borders[stage.index]} scale-100` : 'border-border scale-100'
              }`}
            >
              <Icon
                className={`w-6 h-6 relative z-10 ${
                  isActive
                    ? colors[stage.index]
                    : isPast
                      ? colors[stage.index]
                      : 'text-textMuted'
                }`}
              />
            </div>
            <div className={`font-bold text-sm tracking-tight text-center transition-colors duration-300 ${
              isActive
                ? 'text-text'
                : isPast
                  ? 'text-textSecondary'
                  : 'text-textMuted'
            }`}>
              {stage.label}
            </div>
          </div>
        );
      })}
    </div>
  );
};
